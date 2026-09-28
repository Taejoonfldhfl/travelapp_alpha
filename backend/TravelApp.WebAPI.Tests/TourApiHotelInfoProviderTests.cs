using System.Net;
using System.Text;
using System.Web;
using Microsoft.Extensions.Configuration;
using TravelApp.WebAPI.Services.HotelInfo;

namespace TravelApp.WebAPI.Tests;

// TourAPI 응답 파싱을 고정 JSON으로 검증한다(실제 네트워크 호출 없음). 응답 형태는 KorService2 문서 예시를 따른다.
public class TourApiHotelInfoProviderTests
{
    // 요청 URL을 기록하고, operation(areaBasedList2 등)별로 정해 둔 응답을 돌려준다.
    private sealed class StubTourApiHandler(Dictionary<string, (HttpStatusCode Status, string Body)> responses) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add(request.RequestUri!);
            }

            string operation = request.RequestUri!.AbsolutePath.Split('/').Last();
            var (status, body) = responses.TryGetValue(operation, out var r) ? r : (HttpStatusCode.NotFound, "");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static string Envelope(string itemsJson, string resultCode = "0000", string resultMsg = "OK") =>
        "{\"response\":{\"header\":{\"resultCode\":\"" + resultCode + "\",\"resultMsg\":\"" + resultMsg + "\"}," +
        "\"body\":{\"items\":" + itemsJson + ",\"numOfRows\":20,\"pageNo\":1,\"totalCount\":2}}}";

    private const string TwoHotels = """
        {"item":[
          {"addr1":"서울특별시 중구 세종대로 99","addr2":"(태평로1가)","areacode":"1","contentid":"142785","contenttypeid":"32",
           "firstimage":"http://tong.visitkorea.or.kr/cms/resource/01/1_image2_1.jpg","firstimage2":"http://tong.visitkorea.or.kr/cms/resource/01/1_image3_1.jpg",
           "mapx":"126.9769930325","mapy":"37.5657035718","sigungucode":"24","tel":"02-771-0500","title":"샘플호텔 서울점"},
          {"addr1":"서울특별시 종로구 북촌로 1","addr2":"","contentid":"2000001","contenttypeid":"32",
           "firstimage":"","firstimage2":"http://tong.visitkorea.or.kr/cms/resource/02/2_image3_1.jpg",
           "mapx":"","mapy":"37.58","tel":"02-111-2222<br>010-3333-4444","title":"북촌 &amp; 한옥"}
        ]}
        """;

    private static (TourApiHotelInfoProvider Provider, StubTourApiHandler Handler) Create(
        Dictionary<string, (HttpStatusCode, string)> responses, string serviceKey = "abc+def/ghi==")
    {
        var handler = new StubTourApiHandler(responses);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TourApi:ServiceKey"] = serviceKey })
            .Build();
        return (new TourApiHotelInfoProvider(new HttpClient(handler), configuration), handler);
    }

    private static (TourApiHotelInfoProvider Provider, StubTourApiHandler Handler) CreateWithList(string operation, string body) =>
        Create(new() { [operation] = (HttpStatusCode.OK, body) });

    [Fact]
    public async Task 지역검색_응답을_목록_DTO로_매핑한다()
    {
        var (provider, _) = CreateWithList("areaBasedList2", Envelope(TwoHotels));

        var hotels = await provider.SearchByAreaAsync(new HotelAreaQuery("1"));

        Assert.Equal(2, hotels.Count);
        var first = hotels[0];
        Assert.Equal("142785", first.ContentId);
        Assert.Equal("샘플호텔 서울점", first.Name);
        Assert.Equal("서울특별시 중구 세종대로 99 (태평로1가)", first.Address);
        Assert.Equal("02-771-0500", first.Tel);
        Assert.Equal("http://tong.visitkorea.or.kr/cms/resource/01/1_image2_1.jpg", first.ImageUrl);
        Assert.Equal(37.5657035718, first.Latitude);  // mapy
        Assert.Equal(126.9769930325, first.Longitude); // mapx
    }

    [Fact]
    public async Task mapx가_비어_있으면_좌표만_null이고_나머지는_정상이다()
    {
        var (provider, _) = CreateWithList("areaBasedList2", Envelope(TwoHotels));

        var second = (await provider.SearchByAreaAsync(new HotelAreaQuery("1")))[1];

        Assert.Null(second.Latitude);   // mapy는 멀쩡해도 짝이 없으면 쓰지 않는다
        Assert.Null(second.Longitude);
        Assert.Equal("북촌 & 한옥", second.Name);                              // HTML 엔티티 해제
        Assert.Equal("02-111-2222, 010-3333-4444", second.Tel);               // <br> 정리
        Assert.Equal("http://tong.visitkorea.or.kr/cms/resource/02/2_image3_1.jpg", second.ImageUrl); // firstimage 없으면 firstimage2
        Assert.Equal("서울특별시 종로구 북촌로 1", second.Address);
    }

    [Theory]
    [InlineData("126.97", 180, 126.97)]
    [InlineData(" 37.5 ", 90, 37.5)]
    [InlineData("", 180, null)]
    [InlineData("   ", 180, null)]
    [InlineData(null, 180, null)]
    [InlineData("abc", 180, null)]
    [InlineData("126,97", 180, null)]
    [InlineData("0", 180, null)]
    [InlineData("0.0", 90, null)]
    [InlineData("NaN", 90, null)]
    [InlineData("200.1", 180, null)]
    [InlineData("-91", 90, null)]
    public void ParseCoordinate_해석할_수_없는_값은_null이다(string? raw, double maxAbs, double? expected)
    {
        Assert.Equal(expected, TourApiHotelInfoProvider.ParseCoordinate(raw, maxAbs));
    }

    [Fact]
    public async Task 좌표가_숫자타입으로_와도_해석한다()
    {
        const string items = """{"item":[{"contentid":1,"title":"숫자 좌표 호텔","addr1":"a","mapx":127.1,"mapy":37.2}]}""";
        var (provider, _) = CreateWithList("searchKeyword2", Envelope(items));

        var hotel = Assert.Single(await provider.SearchByKeywordAsync("호텔"));

        Assert.Equal("1", hotel.ContentId);
        Assert.Equal(37.2, hotel.Latitude);
        Assert.Equal(127.1, hotel.Longitude);
    }

    [Fact]
    public async Task 결과가_0건이면_items가_빈문자열이어도_빈목록이다()
    {
        var (provider, _) = CreateWithList("searchKeyword2", Envelope("\"\""));

        Assert.Empty(await provider.SearchByKeywordAsync("없는호텔"));
    }

    [Fact]
    public async Task item이_1건이라_객체로_와도_목록으로_읽는다()
    {
        const string single = """{"item":{"contentid":"77","title":"단일 호텔","addr1":"부산","mapx":"129.16","mapy":"35.15"}}""";
        var (provider, _) = CreateWithList("locationBasedList2", Envelope(single));

        var hotel = Assert.Single(await provider.SearchByLocationAsync(35.15, 129.16, 1000));

        Assert.Equal("77", hotel.ContentId);
    }

    [Fact]
    public async Task 요청에는_숙박_contentTypeId_32와_json_형식과_검색조건이_들어간다()
    {
        var (provider, handler) = Create(new()
        {
            ["areaBasedList2"] = (HttpStatusCode.OK, Envelope("\"\"")),
            ["locationBasedList2"] = (HttpStatusCode.OK, Envelope("\"\"")),
            ["searchKeyword2"] = (HttpStatusCode.OK, Envelope("\"\"")),
        });

        await provider.SearchByAreaAsync(new HotelAreaQuery("1", "24"), pageNo: 2);
        await provider.SearchByLocationAsync(37.5, 126.9, 50_000);
        await provider.SearchByKeywordAsync(" 호텔 서울점 ");

        var area = HttpUtility.ParseQueryString(handler.Requests[0].Query);
        Assert.Equal("/B551011/KorService2/areaBasedList2", handler.Requests[0].AbsolutePath);
        Assert.Equal("32", area["contentTypeId"]);
        Assert.Equal("json", area["_type"]);
        Assert.Equal("1", area["areaCode"]);
        Assert.Equal("24", area["sigunguCode"]);
        Assert.Equal("2", area["pageNo"]);
        Assert.Equal("20", area["numOfRows"]);

        var location = HttpUtility.ParseQueryString(handler.Requests[1].Query);
        Assert.Equal("126.9", location["mapX"]);
        Assert.Equal("37.5", location["mapY"]);
        Assert.Equal("20000", location["radius"]); // TourAPI 상한으로 줄인다
        Assert.Equal("32", location["contentTypeId"]);

        var keyword = HttpUtility.ParseQueryString(handler.Requests[2].Query);
        Assert.Equal("호텔 서울점", keyword["keyword"]);
        Assert.Equal("32", keyword["contentTypeId"]);
    }

    [Theory]
    [InlineData("abc+def/ghi==", "abc%2Bdef%2Fghi%3D%3D")] // Decoding 키 -> 한 번 인코딩
    [InlineData("abc%2Bdef%2Fghi%3D%3D", "abc%2Bdef%2Fghi%3D%3D")] // Encoding 키 -> 그대로(이중 인코딩 방지)
    public async Task 서비스키는_Encoding_Decoding_어느쪽이든_한번만_인코딩된다(string configuredKey, string expectedInUrl)
    {
        var (provider, handler) = Create(new() { ["searchKeyword2"] = (HttpStatusCode.OK, Envelope("\"\"")) }, configuredKey);

        await provider.SearchByKeywordAsync("호텔");

        Assert.Contains($"serviceKey={expectedInUrl}&", handler.Requests[0].OriginalString);
    }

    [Fact]
    public async Task 상세는_detailCommon2와_detailIntro2를_합친다()
    {
        const string common = """
            {"item":[{"contentid":"142785","contenttypeid":"32","title":"샘플호텔 서울점","addr1":"서울특별시 중구 세종대로 99","addr2":"",
             "tel":"02-771-0500","firstimage":"http://tong.visitkorea.or.kr/a.jpg","mapx":"126.97","mapy":"37.56",
             "overview":"도심에 있는 호텔입니다.<br>광화문과 가깝습니다.<br />&lt;참고&gt;"}]}
            """;
        const string intro = """
            {"item":[{"contentid":"142785","contenttypeid":"32","checkintime":"15:00","checkouttime":"12:00",
             "barbecue":"0","fitness":"1","sauna":"1","publicpc":"","subfacility":"레스토랑, 비즈니스센터 / 연회장","parkinglodging":"가능(유료)"}]}
            """;
        var (provider, handler) = Create(new()
        {
            ["detailCommon2"] = (HttpStatusCode.OK, Envelope(common)),
            ["detailIntro2"] = (HttpStatusCode.OK, Envelope(intro)),
        });

        var detail = await provider.GetDetailAsync("142785");

        Assert.NotNull(detail);
        Assert.Equal("샘플호텔 서울점", detail.Name);
        Assert.Equal(37.56, detail.Latitude);
        Assert.Equal("도심에 있는 호텔입니다.\n광화문과 가깝습니다.\n<참고>", detail.Overview);
        Assert.Equal("15:00", detail.CheckInTime);
        Assert.Equal("12:00", detail.CheckOutTime);
        Assert.Equal(["피트니스센터", "사우나", "레스토랑", "비즈니스센터", "연회장", "주차 가능(유료)"], detail.Facilities);

        var introQuery = HttpUtility.ParseQueryString(handler.Requests.Single(r => r.AbsolutePath.EndsWith("detailIntro2")).Query);
        Assert.Equal("32", introQuery["contentTypeId"]);
        Assert.Equal("142785", introQuery["contentId"]);
        Assert.DoesNotContain(handler.Requests, r => r.AbsolutePath.EndsWith("detailInfo2")); // 객실 상세는 범위 밖
    }

    [Fact]
    public async Task 상세에서_intro가_비어도_개요까지는_돌려준다()
    {
        const string common = """{"item":{"contentid":"9","title":"간단 호텔","addr1":"제주","overview":"개요"}}""";
        var (provider, _) = Create(new()
        {
            ["detailCommon2"] = (HttpStatusCode.OK, Envelope(common)),
            ["detailIntro2"] = (HttpStatusCode.OK, Envelope("\"\"")),
        });

        var detail = await provider.GetDetailAsync("9");

        Assert.NotNull(detail);
        Assert.Equal("개요", detail.Overview);
        Assert.Empty(detail.Facilities);
        Assert.Null(detail.Latitude);
    }

    [Fact]
    public async Task 없는_contentId면_null이다()
    {
        var (provider, _) = Create(new()
        {
            ["detailCommon2"] = (HttpStatusCode.OK, Envelope("\"\"")),
            ["detailIntro2"] = (HttpStatusCode.OK, Envelope("\"\"")),
        });

        Assert.Null(await provider.GetDetailAsync("nope"));
    }

    [Fact]
    public async Task resultCode가_0000이_아니면_예외로_알린다()
    {
        var (provider, _) = CreateWithList("searchKeyword2", Envelope("\"\"", "10", "INVALID_REQUEST_PARAMETER_ERROR"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SearchByKeywordAsync("호텔"));
        Assert.Contains("INVALID_REQUEST_PARAMETER_ERROR", ex.Message);
    }

    [Fact]
    public async Task 인증키_오류처럼_XML로_오면_JSON이_아니라고_알린다()
    {
        const string xml = "<OpenAPI_ServiceResponse><cmmMsgHeader><returnAuthMsg>SERVICE_KEY_IS_NOT_REGISTERED_ERROR</returnAuthMsg></cmmMsgHeader></OpenAPI_ServiceResponse>";
        var (provider, _) = CreateWithList("searchKeyword2", xml);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SearchByKeywordAsync("호텔"));
        Assert.Contains("SERVICE_KEY_IS_NOT_REGISTERED_ERROR", ex.Message);
    }

    [Fact]
    public async Task HTTP_오류면_예외로_알린다()
    {
        var (provider, _) = Create(new() { ["searchKeyword2"] = (HttpStatusCode.InternalServerError, "server error") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SearchByKeywordAsync("호텔"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("YOUR_TOURAPI_SERVICE_KEY")]
    public void 서비스키가_없거나_플레이스홀더면_생성시점에_실패한다(string key)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TourApi:ServiceKey"] = key })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => new TourApiHotelInfoProvider(new HttpClient(), configuration));
        Assert.Contains("TourApi:ServiceKey", ex.Message);
    }
}
