using TravelApp.WebAPI.Services.HotelInfo;

namespace TravelApp.WebAPI.Tests;

// TourAPI 키가 없을 때의 기본 provider: 고정 샘플 데이터로 세 가지 검색과 상세를 흉내 낸다.
public class MockHotelInfoProviderTests
{
    private readonly MockHotelInfoProvider _provider = new();

    [Fact]
    public async Task 지역코드로_찾고_시군구코드로_좁힌다()
    {
        var seoul = await _provider.SearchByAreaAsync(new HotelAreaQuery("1"));
        var jongno = await _provider.SearchByAreaAsync(new HotelAreaQuery("1", "23"));

        Assert.Equal(4, seoul.Count);
        Assert.Equal(["샘플 광화문 호텔", "샘플 종로 한옥 게스트하우스"], jongno.Select(h => h.Name).ToArray());
        Assert.Empty(await _provider.SearchByAreaAsync(new HotelAreaQuery("99")));
    }

    [Fact]
    public async Task 위치검색은_반경안의_좌표있는_숙소만_가까운순으로_돌려준다()
    {
        // 광화문 부근에서 2km: 광화문 호텔(약 0.4km), 명동 스테이(약 1.3km). 좌표 없는 한옥은 위치 검색에 나오지 않는다.
        var nearby = await _provider.SearchByLocationAsync(37.5759, 126.9769, 2000);

        Assert.Equal(["샘플 광화문 호텔", "샘플 명동 스테이"], nearby.Select(h => h.Name).ToArray());
        Assert.All(nearby, h => Assert.NotNull(h.Latitude));
    }

    [Theory]
    [InlineData("광화문", 1)]
    [InlineData("해운대", 1)]
    [InlineData("샘플", 6)]
    [InlineData("종로구", 2)]   // 주소로도 찾는다
    [InlineData("명동 스테이", 1)] // 공백 무시
    [InlineData("없는숙소", 0)]
    [InlineData("  ", 0)]
    public async Task 키워드는_이름이나_주소에_포함되면_찾는다(string keyword, int expectedCount)
    {
        Assert.Equal(expectedCount, (await _provider.SearchByKeywordAsync(keyword)).Count);
    }

    [Fact]
    public async Task 좌표없는_샘플은_목록에는_나오고_좌표는_null이다()
    {
        var hanok = Assert.Single(await _provider.SearchByKeywordAsync("한옥"));

        Assert.Null(hanok.Latitude);
        Assert.Null(hanok.Longitude);
        Assert.Null(hanok.ImageUrl);
    }

    [Fact]
    public async Task 상세는_목록정보와_개요_부대시설을_함께_준다()
    {
        var detail = await _provider.GetDetailAsync("mock-1001");

        Assert.NotNull(detail);
        Assert.Equal("샘플 광화문 호텔", detail.Name);
        Assert.Equal(37.5720, detail.Latitude);
        Assert.False(string.IsNullOrWhiteSpace(detail.Overview));
        Assert.Contains("피트니스센터", detail.Facilities);
        Assert.Null(await _provider.GetDetailAsync("unknown"));
    }
}
