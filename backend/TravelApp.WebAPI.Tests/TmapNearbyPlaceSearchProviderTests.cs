using System.Net;
using System.Text;
using System.Web;
using Microsoft.Extensions.Configuration;
using SharedData.DTOs;
using TravelApp.WebAPI.Services.PlaceSearch;
using Xunit;

namespace TravelApp.WebAPI.Tests
{
    // Tmap 요청 형식 회귀 테스트. 실제 API 없이 요청 URL과 응답 매핑만 확인한다.
    public class TmapNearbyPlaceSearchProviderTests
    {
        private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
        {
            public List<Uri> Requests { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request.RequestUri!);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
                });
            }
        }

        private const string OnePoi = """
            {"searchPoiInfo":{"pois":{"poi":[{"name":"우육면관 청계천점[중식]","noorLat":"37.5690","noorLon":"126.9860",
             "upperAddrName":"서울","middleAddrName":"종로구","lowerAddrName":"관철동","upperBizName":"생활편의","middleBizName":"음식점","lowerBizName":"중식"}]}}}
            """;

        private static (TmapNearbyPlaceSearchProvider Provider, RecordingHandler Handler) Create()
        {
            var handler = new RecordingHandler(OnePoi);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Tmap:AppKey"] = "test-key" })
                .Build();
            return (new TmapNearbyPlaceSearchProvider(new HttpClient(handler), configuration), handler);
        }

        [Fact]
        public async Task 반경은_정수_km로_올림하고_카테고리와_개수를_요청에_넣는다()
        {
            // 실측: Tmap은 radius=1.5 같은 소수를 400(1100)으로 거절한다.
            var (provider, handler) = Create();

            await provider.SearchNearbyAsync(new NearbyPlaceSearchRequest(37.5, 127.0, 1500, null, PlaceCategory.Restaurant, 40));

            var query = HttpUtility.ParseQueryString(Assert.Single(handler.Requests).Query);
            Assert.Equal("2", query["radius"]);
            Assert.Equal("음식점", query["categories"]);
            Assert.Equal("40", query["count"]);
        }

        [Fact]
        public async Task 세부_업종_키워드가_있으면_카테고리보다_우선한다()
        {
            var (provider, handler) = Create();

            await provider.SearchNearbyAsync(new NearbyPlaceSearchRequest(37.5, 127.0, 1000, "한식", PlaceCategory.Restaurant));

            var query = HttpUtility.ParseQueryString(Assert.Single(handler.Requests).Query);
            Assert.Equal("한식", query["categories"]);
            Assert.Equal("20", query["count"]); // 개수를 안 주면 기존 기본값
        }

        [Fact]
        public async Task 응답의_영업상태는_Unknown이고_표시이름에서_업종표기를_뗀다()
        {
            var (provider, _) = Create();

            var place = Assert.Single(await provider.SearchNearbyAsync(new NearbyPlaceSearchRequest(37.5, 127.0, 1000, null)));

            Assert.Equal(PlaceOperatingStatus.Unknown, place.OperatingStatus);
            Assert.Equal("우육면관 청계천점", place.CanonicalName);
            Assert.Equal("우육면관 청계천점[중식]", place.Name);
            Assert.Equal("Tmap", place.Source);
        }

        [Fact]
        public async Task 메뉴_검색어가_있으면_중심점_주변_키워드검색을_쓴다()
        {
            // 실측: 업종 검색에 '파스타'를 넣으면 0건, 통합검색 + 중심점 + searchtypCd=R이면 파스타 가게가 나온다.
            var (provider, handler) = Create();

            await provider.SearchNearbyAsync(new NearbyPlaceSearchRequest(37.4979, 127.0276, 2000, null,
                PlaceCategory.Restaurant, 40, SearchText: "파스타"));

            var uri = Assert.Single(handler.Requests);
            var query = HttpUtility.ParseQueryString(uri.Query);
            Assert.Equal("/tmap/pois", uri.AbsolutePath);
            Assert.Equal("파스타", query["searchKeyword"]);
            Assert.Equal("R", query["searchtypCd"]);
            Assert.Equal("37.4979", query["centerLat"]);
            Assert.Equal("127.0276", query["centerLon"]);
            Assert.Equal("2", query["radius"]);
            Assert.Null(query["categories"]);
        }
    }
}
