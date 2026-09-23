using System.Globalization;
using System.Text.Json;
using System.Web;
using SharedData.DTOs;

namespace TravelApp.WebAPI.Services.PlaceSearch
{
    // Tmap(SK Open API) POI 검색 연동. 공식 문서(https://skopenapi.readme.io)로 확인한 두 엔드포인트를 쓴다.
    //
    //  - SearchByNameAsync: "장소(POI) 통합검색" GET /tmap/pois (searchKeyword).
    //    응답 구조(searchPoiInfo.pois.poi[].name/noorLat/noorLon/...)가 문서에 예시와 함께 명확히
    //    나와 있어 이 엔드포인트를 그대로 썼다. 문서에는 searchType=name도 쓸 수 있다고 나오지만,
    //    실제 호출해보면 400(9401 "필수 파라메터가 없습니다")이 나서 뺐다 — searchType을 생략하면
    //    기본값(all, 통합검색)으로 정상 동작하고 이름 관련성 높은 순으로 잘 나온다(실측 확인됨).
    //  - SearchNearbyAsync: "명칭(POI) 주변 카테고리 검색" GET /tmap/pois/search/around
    //    (centerLon/centerLat 필수, categories는 선택 — 카테고리 없이 "주변 전체"도 가능해서
    //    Keyword가 없는 일반 "근처" 검색에 이 엔드포인트가 더 맞다). 다만 이 엔드포인트는 문서에
    //    요청 파라미터만 나와 있고 응답 예시가 없어서, 통합검색과 같은 SK POI 패밀리이므로
    //    응답 구조가 /tmap/pois와 동일(searchPoiInfo.pois.poi[])하다고 가정하고 구현했다.
    //    만약 실제 호출에서 이 가정이 틀리면 ParsePois에서 명확한 예외가 나므로 바로 알 수 있다.
    //
    // 중요한 한계: Tmap POI 응답에는 영업상태/폐업 여부 필드가 없다(문서에서 확인). 그래서 여기서
    // 만든 결과는 항상 IsPermanentlyClosed=false, LastConfirmedOperatingDate=null(=방금 검색으로
    // 확인된 최신 정보로 취급)로 채운다. 폐업 필터링은 여전히 프롬프트로 LLM에게 피하라고 유도하는
    // 수준까지만 보장되고, Tmap 자체가 폐업을 알려주지는 않는다.
    public class TmapNearbyPlaceSearchProvider : INearbyPlaceSearchProvider
    {
        private const string PoiSearchEndpoint = "https://apis.openapi.sk.com/tmap/pois";
        private const string PoiAroundSearchEndpoint = "https://apis.openapi.sk.com/tmap/pois/search/around";

        // /tmap/pois/search/around의 radius는 1~33km(0=전국). 우리 반경은 미터 단위라 변환/clamp한다.
        private const double MinRadiusKm = 1;
        private const double MaxRadiusKm = 33;

        // 주변 검색 결과 개수. 요청에 개수가 없으면 기존과 같은 20건, 요청하더라도 50건을 넘기지 않는다(쿼터/응답 크기).
        private const int DefaultNearbyCount = 20;
        private const int MaxNearbyCount = 50;

        private const string SourceName = "Tmap";

        private readonly HttpClient _httpClient;
        private readonly string _appKey;

        public TmapNearbyPlaceSearchProvider(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;

            _appKey = configuration["Tmap:AppKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_appKey))
            {
                throw new InvalidOperationException(
                    "Tmap:AppKey가 설정되지 않았습니다. SK Open API에서 발급받은 앱키를 다음 명령으로 설정하세요:\n" +
                    "  dotnet user-secrets set \"Tmap:AppKey\" \"<YOUR_APP_KEY>\" --project backend/TravelApp.WebAPI");
            }
        }

        public async Task<List<PlaceSearchResultDto>> SearchNearbyAsync(
            NearbyPlaceSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            // Tmap은 radius에 정수(km)만 받는다 — 1.5처럼 소수를 보내면 400(1100 "데이터 형식이 틀립니다")이 난다(실측 확인).
            // 요청 반경보다 좁아지지 않도록 올림한다.
            double radiusKm = Math.Clamp(Math.Ceiling(request.RadiusMeters / 1000.0), MinRadiusKm, MaxRadiusKm);

            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                return await SearchKeywordAroundAsync(request, radiusKm, cancellationToken);
            }

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["version"] = "1";
            query["centerLon"] = request.Longitude.ToString(CultureInfo.InvariantCulture);
            query["centerLat"] = request.Latitude.ToString(CultureInfo.InvariantCulture);
            query["radius"] = radiusKm.ToString(CultureInfo.InvariantCulture);
            query["count"] = Math.Clamp(request.MaxResults ?? DefaultNearbyCount, 1, MaxNearbyCount).ToString(CultureInfo.InvariantCulture);
            query["page"] = "1";
            query["sort"] = "distance";

            // Tmap categories 파라미터는 한글 업종명을 세미콜론으로 구분해 받는다. 세부 업종(Keyword, 예: '한식')이
            // 있으면 그걸, 없으면 카테고리에 대응하는 업종명을 쓴다(실측 확인: 음식점/카페/술집/관광명소/쇼핑/호텔/한식 동작).
            string? categories = !string.IsNullOrWhiteSpace(request.Keyword)
                ? request.Keyword
                : ToTmapCategories(request.Category);

            if (categories != null)
            {
                query["categories"] = categories;
            }

            string url = $"{PoiAroundSearchEndpoint}?{query}";
            string body = await CallTmapAsync(url, cancellationToken);

            return ParsePois(body, place => true);
        }

        // 관련성 1순위가 찾던 곳이 아닌 경우가 실제로 있어서(예: "몽로" 검색 시 1순위 '카페로몽',
        // 2순위 '몽로') 여러 건을 받는다. 순서는 Tmap이 준 관련성 순위 그대로 두고 재정렬하지 않는다.
        // 알려진 한계: Tmap 응답에 휴관 정보가 없어(예: 이름에 '휴관중'이 붙은 국립한글박물관도 영업 중으로
        // 취급됨) 휴관/폐업 판별에는 별도 데이터소스가 필요하다.
        public async Task<IReadOnlyList<PlaceSearchResultDto>> SearchByNameAsync(
            string placeName,
            CancellationToken cancellationToken = default)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["version"] = "1";
            query["searchKeyword"] = placeName;
            query["count"] = INearbyPlaceSearchProvider.MaxNameSearchResults.ToString(CultureInfo.InvariantCulture);
            query["page"] = "1";

            string url = $"{PoiSearchEndpoint}?{query}";
            string body = await CallTmapAsync(url, cancellationToken);

            var results = ParsePois(body, place => true)
                .Take(INearbyPlaceSearchProvider.MaxNameSearchResults)
                .ToList();

            // 관련성 순위가 곧 신뢰도다(1순위 1.0부터 순위마다 낮아짐). 순서는 바꾸지 않는다.
            for (int i = 0; i < results.Count; i++)
            {
                results[i].SearchConfidence = PlaceNames.RankConfidence(i);
            }

            return results;
        }

        private async Task<string> CallTmapAsync(string url, CancellationToken cancellationToken)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            httpRequest.Headers.Add("appKey", _appKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Tmap POI 검색 API 호출이 실패했습니다 ({(int)response.StatusCode} {response.StatusCode}): {body}");
            }

            return body;
        }

        // '파스타'처럼 업종명이 아닌 검색어는 주변 업종 검색(categories)으로 찾을 수 없어(0건), 통합검색(/tmap/pois)에
        // 중심점·반경을 주고 거리순(searchtypCd=R)으로 찾는다. 실측: 강남역 반경 2km '파스타' -> 63건(파스타 가게 위주).
        private async Task<List<PlaceSearchResultDto>> SearchKeywordAroundAsync(
            NearbyPlaceSearchRequest request, double radiusKm, CancellationToken cancellationToken)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["version"] = "1";
            query["searchKeyword"] = request.SearchText;
            query["searchtypCd"] = "R";
            query["centerLon"] = request.Longitude.ToString(CultureInfo.InvariantCulture);
            query["centerLat"] = request.Latitude.ToString(CultureInfo.InvariantCulture);
            query["radius"] = radiusKm.ToString(CultureInfo.InvariantCulture);
            query["count"] = Math.Clamp(request.MaxResults ?? DefaultNearbyCount, 1, MaxNearbyCount).ToString(CultureInfo.InvariantCulture);
            query["page"] = "1";

            string body = await CallTmapAsync($"{PoiSearchEndpoint}?{query}", cancellationToken);
            return ParsePois(body, place => true);
        }

        private static string? ToTmapCategories(PlaceCategory category) => category switch
        {
            PlaceCategory.Restaurant => "음식점",
            PlaceCategory.Cafe => "카페",
            PlaceCategory.Bar => "술집",
            PlaceCategory.Attraction => "관광명소",
            PlaceCategory.Shopping => "쇼핑",
            PlaceCategory.Hotel => "호텔",
            _ => null
        };

        // searchPoiInfo.pois.poi[] 배열을 PlaceSearchResultDto 목록으로 매핑한다.
        // 문서가 예시를 준 통합검색(/tmap/pois) 응답 구조를 기준으로 하며, 주변 카테고리 검색도
        // 같은 구조라고 가정한다 — 구조가 다르면 아래에서 명확한 예외가 발생한다.
        private static List<PlaceSearchResultDto> ParsePois(string responseBody, Func<PlaceSearchResultDto, bool> filter)
        {
            // 결과가 0건이면 Tmap이 본문 없는 204 No Content를 준다(실측 확인됨) — 이 경우 그냥 빈 목록.
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return new List<PlaceSearchResultDto>();
            }

            using var json = JsonDocument.Parse(responseBody);

            if (!json.RootElement.TryGetProperty("searchPoiInfo", out var searchPoiInfo))
            {
                throw new InvalidOperationException(
                    "Tmap POI 검색 응답에 searchPoiInfo가 없습니다. API 응답 형식이 예상과 다릅니다: " + responseBody);
            }

            var results = new List<PlaceSearchResultDto>();

            if (!searchPoiInfo.TryGetProperty("pois", out var poisElement) ||
                !poisElement.TryGetProperty("poi", out var poiArray) ||
                poiArray.ValueKind != JsonValueKind.Array)
            {
                // 결과 0건인 경우 pois/poi가 아예 없을 수 있다 — 빈 목록으로 처리한다.
                return results;
            }

            foreach (var poi in poiArray.EnumerateArray())
            {
                string name = GetString(poi, "name");

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                double? lat = GetNullableDouble(poi, "noorLat") ?? GetNullableDouble(poi, "frontLat");
                double? lon = GetNullableDouble(poi, "noorLon") ?? GetNullableDouble(poi, "frontLon");

                if (lat == null || lon == null)
                {
                    continue;
                }

                string category = string.Join(" > ", new[]
                {
                    GetString(poi, "upperBizName"),
                    GetString(poi, "middleBizName"),
                    GetString(poi, "lowerBizName")
                }.Where(s => !string.IsNullOrWhiteSpace(s)));

                string address = string.Join(" ", new[]
                {
                    GetString(poi, "upperAddrName"),
                    GetString(poi, "middleAddrName"),
                    GetString(poi, "lowerAddrName")
                }.Where(s => !string.IsNullOrWhiteSpace(s)));

                // Tmap의 id 필드는 부속 시설물(주차장 등)이 본체 시설과 같은 값을 공유하는 경우가
                // 있어(실측 확인됨) 그대로 PlaceId로 쓰면 서로 다른 장소가 충돌한다. 이름+좌표
                // 조합을 그대로 쓴다.
                var place = new PlaceSearchResultDto
                {
                    PlaceId = $"tmap:{name}:{lat.Value:F5},{lon.Value:F5}",
                    Name = name,
                    CanonicalName = PlaceNames.ToCanonicalName(name),
                    Category = category,
                    Address = address,
                    Latitude = lat.Value,
                    Longitude = lon.Value,
                    Source = SourceName,
                    // Tmap POI 응답에는 영업상태/폐업 정보가 없다 — 영업 중이라고 볼 근거가 없으므로 Unknown이다
                    // (추천 후보에서 빼지는 않지만 영업 중으로 표현하지 않는다).
                    OperatingStatus = PlaceOperatingStatus.Unknown,
                    IsPermanentlyClosed = false,
                    LastConfirmedOperatingDate = null
                };

                if (filter(place))
                {
                    results.Add(place);
                }
            }

            return results;
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return string.Empty;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.ToString(),
                _ => string.Empty
            };
        }

        // Tmap 응답은 위경도 같은 숫자 값을 문자열로 내려주는 경우가 흔해서(공공/준공공 API 관례),
        // 문자열/숫자 양쪽 다 방어적으로 처리한다.
        private static double? GetNullableDouble(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number => value.GetDouble(),
                JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null
            };
        }
    }
}
