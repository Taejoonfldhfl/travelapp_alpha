using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using SharedData.DTOs;

namespace TravelApp.WebAPI.Services.HotelInfo
{
    // 한국관광공사 TourAPI 4.0(국문 관광정보 서비스, KorService2) 숙박시설 조회.
    //  - 목록: areaBasedList2 / locationBasedList2 / searchKeyword2 (모두 contentTypeId=32 숙박)
    //  - 상세: detailCommon2(개요, 대표이미지) + detailIntro2(체크인/아웃, 부대시설). 객실별 detailInfo2는 쓰지 않는다.
    // 응답 형태에서 조심할 점:
    //  - 결과가 0건이면 body.items가 객체가 아니라 빈 문자열("")로 온다.
    //  - item이 1건이면 배열이 아니라 객체로 오는 경우가 있다.
    //  - mapx/mapy가 문자열("126.97...")로 오고, 빈 문자열/"0"/깨진 값일 수 있다 -> 해석 못 하면 좌표 null.
    //  - 인증키 오류 등은 _type=json을 줘도 XML로 올 수 있다 -> JSON이 아니면 명확한 예외.
    // 서비스키: data.go.kr이 주는 "Encoding" 키(%가 들어 있음)와 "Decoding" 키 어느 쪽을 넣어도 동작하도록,
    // 이미 인코딩된 키는 그대로, 아니면 URL 인코딩해서 붙인다(이중 인코딩되면 SERVICE_KEY_IS_NOT_REGISTERED 오류).
    public class TourApiHotelInfoProvider : IHotelInfoProvider
    {
        private const string BaseUrl = "https://apis.data.go.kr/B551011/KorService2";
        private const string LodgingContentTypeId = "32";
        private const string MobileOs = "AND";
        private const string MobileApp = "TravelApp";
        private const string SuccessResultCode = "0000";

        // locationBasedList2의 radius 상한(m).
        public const int MaxRadiusMeters = 20000;

        // detailIntro2 숙박 항목 중 "1"이면 있는 시설인 플래그들.
        private static readonly (string Field, string Label)[] FacilityFlags =
        [
            ("barbecue", "바비큐장"),
            ("beauty", "뷰티시설"),
            ("beverage", "식음료장"),
            ("bicycle", "자전거 대여"),
            ("campfire", "캠프파이어"),
            ("fitness", "피트니스센터"),
            ("karaoke", "노래방"),
            ("publicbath", "공용 샤워실"),
            ("publicpc", "공용 PC실"),
            ("sauna", "사우나"),
            ("seminar", "세미나실"),
            ("sports", "스포츠 시설"),
        ];

        private static readonly Regex LineBreakTag = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AnyTag = new(@"<[^>]+>", RegexOptions.Compiled);

        private readonly HttpClient _httpClient;
        private readonly string _encodedServiceKey;

        public TourApiHotelInfoProvider(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;

            string serviceKey = configuration["TourApi:ServiceKey"]?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(serviceKey) || serviceKey.Contains(SecretsConfigurationCheck.PlaceholderMarker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "TourApi:ServiceKey가 설정되지 않았습니다. 공공데이터포털에서 발급받은 한국관광공사 TourAPI 인증키를 다음 명령으로 설정하세요:\n" +
                    "  dotnet user-secrets set \"TourApi:ServiceKey\" \"<YOUR_SERVICE_KEY>\" --project backend/TravelApp.WebAPI\n" +
                    "(운영에서는 환경변수 TourApi__ServiceKey)");
            }

            _encodedServiceKey = serviceKey.Contains('%') ? serviceKey : Uri.EscapeDataString(serviceKey);
        }

        public async Task<IReadOnlyList<HotelListItemDto>> SearchByAreaAsync(
            HotelAreaQuery area, int pageNo = 1, CancellationToken cancellationToken = default)
        {
            var query = ListQuery(pageNo);
            query["areaCode"] = area.AreaCode;
            if (!string.IsNullOrWhiteSpace(area.SigunguCode))
            {
                query["sigunguCode"] = area.SigunguCode;
            }

            return ParseList(await CallAsync("areaBasedList2", query, cancellationToken));
        }

        public async Task<IReadOnlyList<HotelListItemDto>> SearchByLocationAsync(
            double latitude, double longitude, int radiusMeters, int pageNo = 1, CancellationToken cancellationToken = default)
        {
            var query = ListQuery(pageNo);
            query["mapX"] = longitude.ToString(CultureInfo.InvariantCulture);
            query["mapY"] = latitude.ToString(CultureInfo.InvariantCulture);
            query["radius"] = Math.Clamp(radiusMeters, 1, MaxRadiusMeters).ToString(CultureInfo.InvariantCulture);
            query["arrange"] = "E"; // 거리순

            return ParseList(await CallAsync("locationBasedList2", query, cancellationToken));
        }

        public async Task<IReadOnlyList<HotelListItemDto>> SearchByKeywordAsync(
            string keyword, int pageNo = 1, CancellationToken cancellationToken = default)
        {
            var query = ListQuery(pageNo);
            query["keyword"] = keyword.Trim();

            return ParseList(await CallAsync("searchKeyword2", query, cancellationToken));
        }

        public async Task<HotelDetailDto?> GetDetailAsync(string contentId, CancellationToken cancellationToken = default)
        {
            var commonQuery = HttpUtility.ParseQueryString(string.Empty);
            commonQuery["contentId"] = contentId;

            var introQuery = HttpUtility.ParseQueryString(string.Empty);
            introQuery["contentId"] = contentId;
            introQuery["contentTypeId"] = LodgingContentTypeId;

            var commonTask = CallAsync("detailCommon2", commonQuery, cancellationToken);
            var introTask = CallAsync("detailIntro2", introQuery, cancellationToken);
            await Task.WhenAll(commonTask, introTask);

            var common = ReadItems(await commonTask).FirstOrDefault();
            if (common.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var detail = new HotelDetailDto();
            FillListFields(detail, common);
            detail.Overview = CleanText(GetString(common, "overview"), lineBreak: "\n");

            var intro = ReadItems(await introTask).FirstOrDefault();
            if (intro.ValueKind == JsonValueKind.Object)
            {
                detail.CheckInTime = CleanText(GetString(intro, "checkintime"), lineBreak: " ");
                detail.CheckOutTime = CleanText(GetString(intro, "checkouttime"), lineBreak: " ");
                detail.Facilities = ReadFacilities(intro);
            }

            return detail;
        }

        // ---- 요청 ----

        private static System.Collections.Specialized.NameValueCollection ListQuery(int pageNo)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["numOfRows"] = IHotelInfoProvider.PageSize.ToString(CultureInfo.InvariantCulture);
            query["pageNo"] = Math.Max(1, pageNo).ToString(CultureInfo.InvariantCulture);
            query["contentTypeId"] = LodgingContentTypeId;
            return query;
        }

        private async Task<string> CallAsync(
            string operation, System.Collections.Specialized.NameValueCollection query, CancellationToken cancellationToken)
        {
            query["MobileOS"] = MobileOs;
            query["MobileApp"] = MobileApp;
            query["_type"] = "json";

            // serviceKey는 HttpUtility가 다시 인코딩하지 않도록 따로 붙인다.
            string url = $"{BaseUrl}/{operation}?serviceKey={_encodedServiceKey}&{query}";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"TourAPI {operation} 호출이 실패했습니다 ({(int)response.StatusCode} {response.StatusCode}): {Truncate(body)}");
            }

            return body;
        }

        // ---- 응답 해석 ----

        private static List<HotelListItemDto> ParseList(string body) =>
            ReadItems(body)
                .Select(item =>
                {
                    var dto = new HotelListItemDto();
                    FillListFields(dto, item);
                    return dto;
                })
                .Where(dto => dto.ContentId.Length > 0 && dto.Name.Length > 0)
                .ToList();

        // response.header.resultCode를 확인하고 response.body.items.item을 배열로 돌려준다(0건이면 빈 목록).
        private static List<JsonElement> ReadItems(string body)
        {
            string trimmed = body.TrimStart();
            if (!trimmed.StartsWith('{'))
            {
                throw new InvalidOperationException("TourAPI 응답이 JSON이 아닙니다(인증키/요청 오류일 수 있음): " + Truncate(body));
            }

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            if (!root.TryGetProperty("response", out var response))
            {
                // 게이트웨이 오류는 {"resultCode":"..","resultMsg":".."} 형태로 오기도 한다.
                throw new InvalidOperationException(
                    $"TourAPI 오류 응답입니다: {GetString(root, "resultCode") ?? "-"} {GetString(root, "resultMsg") ?? Truncate(body)}");
            }

            if (response.TryGetProperty("header", out var header))
            {
                string? resultCode = GetString(header, "resultCode");
                if (resultCode != null && resultCode != SuccessResultCode)
                {
                    throw new InvalidOperationException($"TourAPI 오류 응답입니다: {resultCode} {GetString(header, "resultMsg")}");
                }
            }

            if (!response.TryGetProperty("body", out var responseBody) ||
                !responseBody.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Object ||
                !items.TryGetProperty("item", out var item))
            {
                return new List<JsonElement>(); // 0건: items == ""
            }

            // JsonDocument를 닫은 뒤에도 쓸 수 있게 복제한다.
            return item.ValueKind switch
            {
                JsonValueKind.Array => item.EnumerateArray().Select(e => e.Clone()).ToList(),
                JsonValueKind.Object => new List<JsonElement> { item.Clone() },
                _ => new List<JsonElement>()
            };
        }

        private static void FillListFields(HotelListItemDto dto, JsonElement item)
        {
            dto.ContentId = GetString(item, "contentid") ?? string.Empty;
            dto.Name = CleanText(GetString(item, "title"), lineBreak: " ") ?? string.Empty;
            dto.Address = string.Join(" ", new[] { GetString(item, "addr1"), GetString(item, "addr2") }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim()));
            dto.Tel = CleanText(GetString(item, "tel"), lineBreak: ", ");
            dto.ImageUrl = NonEmpty(GetString(item, "firstimage")) ?? NonEmpty(GetString(item, "firstimage2"));

            // 좌표는 둘 다 해석될 때만 쓴다(하나라도 깨지면 지도 마커 없이 목록에만 표시).
            var (latitude, longitude) = (ParseCoordinate(GetString(item, "mapy"), 90), ParseCoordinate(GetString(item, "mapx"), 180));
            if (latitude != null && longitude != null)
            {
                dto.Latitude = latitude;
                dto.Longitude = longitude;
            }
        }

        // mapx/mapy 문자열을 좌표로. 비어 있거나, 숫자가 아니거나, 0(미등록)이거나, 범위를 벗어나면 null.
        public static double? ParseCoordinate(string? value, double maxAbs)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ||
                double.IsNaN(parsed) || double.IsInfinity(parsed) || parsed == 0 || Math.Abs(parsed) > maxAbs)
            {
                return null;
            }

            return parsed;
        }

        private static List<string> ReadFacilities(JsonElement intro)
        {
            var facilities = FacilityFlags
                .Where(f => GetString(intro, f.Field)?.Trim() == "1")
                .Select(f => f.Label)
                .ToList();

            // subfacility는 "레스토랑, 비즈니스센터"처럼 쉼표로 나열된 자유 텍스트다.
            string? subFacility = CleanText(GetString(intro, "subfacility"), lineBreak: ",");
            if (subFacility != null)
            {
                facilities.AddRange(subFacility
                    .Split([',', '/', '·'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            string? parking = CleanText(GetString(intro, "parkinglodging"), lineBreak: " ");
            if (parking != null)
            {
                facilities.Add($"주차 {parking}");
            }

            return facilities.Distinct(StringComparer.Ordinal).ToList();
        }

        // 숫자로 오는 필드(contentid 등)도 문자열로 읽는다.
        private static string? GetString(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            };
        }

        // TourAPI 텍스트에는 <br>, <a> 같은 HTML과 &amp; 같은 엔티티가 섞여 온다.
        private static string? CleanText(string? value, string lineBreak)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string text = LineBreakTag.Replace(value, lineBreak);
            text = AnyTag.Replace(text, string.Empty);
            text = WebUtility.HtmlDecode(text).Trim();
            return text.Length == 0 ? null : text;
        }

        private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string Truncate(string body) => body.Length <= 300 ? body : body[..300] + "...";
    }
}
