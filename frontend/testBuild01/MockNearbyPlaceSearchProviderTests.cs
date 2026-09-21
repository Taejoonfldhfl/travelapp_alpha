using TravelApp.WebAPI.Services.PlaceSearch;
using Xunit;

namespace TravelApp.WebAPI.Tests
{
    // 규칙 1(GPS 기반 근처 검색)에서 쓰는 mock 장소 검색 provider가
    // "좌표가 확정된 개별 장소 목록"을 실제로 돌려주는지 검증한다.
    public class MockNearbyPlaceSearchProviderTests
    {
        private const double SeoulStationLat = 37.5547;
        private const double SeoulStationLng = 126.9707;

        [Fact]
        public async Task 근처검색_성공_시나리오_반경내_장소는_좌표를_갖고_반환된다()
        {
            var provider = new MockNearbyPlaceSearchProvider();

            var results = await provider.SearchNearbyAsync(
                new NearbyPlaceSearchRequest(SeoulStationLat, SeoulStationLng, RadiusMeters: 1000, Keyword: null));

            Assert.NotEmpty(results);
            Assert.All(results, place =>
            {
                Assert.False(string.IsNullOrWhiteSpace(place.PlaceId));
                Assert.False(string.IsNullOrWhiteSpace(place.Name));
                Assert.NotEqual(0, place.Latitude);
                Assert.NotEqual(0, place.Longitude);
            });
        }

        [Fact]
        public async Task 반경보다_먼_템플릿은_결과에서_빠진다()
        {
            var provider = new MockNearbyPlaceSearchProvider();

            var wideResults = await provider.SearchNearbyAsync(
                new NearbyPlaceSearchRequest(SeoulStationLat, SeoulStationLng, RadiusMeters: 2000, Keyword: null));
            var narrowResults = await provider.SearchNearbyAsync(
                new NearbyPlaceSearchRequest(SeoulStationLat, SeoulStationLng, RadiusMeters: 100, Keyword: null));

            Assert.True(narrowResults.Count < wideResults.Count);
        }

        [Fact]
        public async Task 카페_키워드로_검색하면_카페_카테고리만_반환된다()
        {
            var provider = new MockNearbyPlaceSearchProvider();

            var results = await provider.SearchNearbyAsync(
                new NearbyPlaceSearchRequest(SeoulStationLat, SeoulStationLng, RadiusMeters: 2000, Keyword: "카페"));

            Assert.NotEmpty(results);
            Assert.All(results, place => Assert.Equal("카페", place.Category));
        }

        // 사용자가 "경복궁 근처 식당" 같은 특정 장소 기준 요청을 했을 때 쓰는 이름 검색.
        // 실제로 존재하는(mock 세계에서는 Templates에 있는) 이름은 좌표와 함께 찾아진다.
        [Fact]
        public async Task 특정장소기준_시나리오_실존하는_이름은_좌표와_함께_찾아진다()
        {
            var provider = new MockNearbyPlaceSearchProvider();

            var result = await provider.SearchByNameAsync("스타벅스 리저브점");

            Assert.NotNull(result);
            Assert.Equal("스타벅스 리저브점", result!.Name);
            Assert.NotEqual(0, result.Latitude);
            Assert.NotEqual(0, result.Longitude);
        }

        // 환각(존재하지 않는 이름)은 이름 검색으로도 걸러져야 한다.
        [Fact]
        public async Task 환각_시나리오_존재하지_않는_이름은_null을_반환한다()
        {
            var provider = new MockNearbyPlaceSearchProvider();

            var result = await provider.SearchByNameAsync("이세상에없는가상의맛집12345");

            Assert.Null(result);
        }
    }
}
