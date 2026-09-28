using TravelApp.WebAPI.Services.PlaceImage;

namespace TravelApp.WebAPI.Tests;

// 사진 API 키가 없는 동안의 기본 provider: 어떤 장소든 사진 없음(null)을 돌려주고, 가짜 URL을 만들지 않는다.
public class MockPlaceImageProviderTests
{
    [Theory]
    [InlineData("경복궁", 37.5796, 126.9770)]
    [InlineData("진짜 식당", null, null)]
    [InlineData("", null, null)]
    public async Task 항상_null을_돌려준다(string placeName, double? latitude, double? longitude)
    {
        IPlaceImageProvider provider = new MockPlaceImageProvider();

        Assert.Null(await provider.GetRepresentativeImageUrlAsync(placeName, latitude, longitude));
    }

    [Fact]
    public async Task 취소된_토큰이어도_외부_호출이_없어_예외없이_null이다()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Null(await new MockPlaceImageProvider().GetRepresentativeImageUrlAsync("경복궁", null, null, cts.Token));
    }
}
