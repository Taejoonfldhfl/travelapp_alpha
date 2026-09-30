using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SharedData.DTOs;
using TravelApp.WebAPI.Services.PlaceStatus;

namespace TravelApp.WebAPI.Tests;

// TourApiHotelInfoProviderTests와 같은 스타일: HttpMessageHandler를 스텁으로 응답을 고정하고 실제 네트워크 호출은 하지 않는다.
public class GooglePlacesOperatingStatusProviderTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content?.ReadAsStringAsync(cancellationToken).Result;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw exception;
    }

    private static (GooglePlacesOperatingStatusProvider Provider, StubHandler Handler) Create(
        HttpStatusCode status, string body, string apiKey = "test-api-key")
    {
        var handler = new StubHandler(status, body);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Google:PlacesApiKey"] = apiKey })
            .Build();
        var provider = new GooglePlacesOperatingStatusProvider(
            new HttpClient(handler), configuration, NullLogger<GooglePlacesOperatingStatusProvider>.Instance);
        return (provider, handler);
    }

    // displayName은 기본으로 질의("경복궁")와 같게 줘서, 이름 유사도 검증을 통과한 상태로 businessStatus
    // 매핑 로직만 따로 검증할 수 있게 한다.
    private static string PlacesResponse(string businessStatus, string displayName = "경복궁") =>
        JsonSerializer.Serialize(new { places = new[] { new { businessStatus, displayName = new { text = displayName } } } });

    [Theory]
    [InlineData("OPERATIONAL", PlaceOperatingStatus.Open, false)]
    [InlineData("CLOSED_TEMPORARILY", PlaceOperatingStatus.Closed, false)]
    [InlineData("CLOSED_PERMANENTLY", PlaceOperatingStatus.Closed, true)]
    public async Task businessStatus를_영업상태로_매핑한다(string businessStatus, PlaceOperatingStatus expectedStatus, bool expectedPermanentlyClosed)
    {
        var (provider, _) = Create(HttpStatusCode.OK, PlacesResponse(businessStatus));

        var result = await provider.GetOperatingStatusAsync("경복궁", 37.58, 126.98);

        Assert.NotNull(result);
        Assert.Equal(expectedStatus, result!.Status);
        Assert.Equal(expectedPermanentlyClosed, result.IsPermanentlyClosed);
    }

    [Fact]
    public async Task 결과가_없으면_null이다()
    {
        var (provider, _) = Create(HttpStatusCode.OK, JsonSerializer.Serialize(new { places = Array.Empty<object>() }));

        Assert.Null(await provider.GetOperatingStatusAsync("존재하지않는곳", 37.58, 126.98));
    }

    [Fact]
    public async Task businessStatus_필드가_없으면_null이다()
    {
        var (provider, _) = Create(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { places = new[] { new { displayName = new { text = "경복궁" } } } }));

        Assert.Null(await provider.GetOperatingStatusAsync("경복궁", 37.58, 126.98));
    }

    [Fact]
    public async Task 매칭된_장소_이름이_질의와_다르면_businessStatus를_신뢰하지_않고_null이다()
    {
        // 실 API 테스트로 확인된 사례 재현: "가상현실맛집XYZ" 질의에 무관한 장소("Starfield Coex Mall")가
        // matches[0]으로 오고 businessStatus=OPERATIONAL이더라도, 이름이 전혀 다르므로 신뢰해서는 안 된다.
        var (provider, _) = Create(HttpStatusCode.OK, PlacesResponse("OPERATIONAL", displayName: "Starfield Coex Mall"));

        Assert.Null(await provider.GetOperatingStatusAsync("가상현실맛집XYZ", 37.58, 126.98));
    }

    [Fact]
    public async Task HTTP_오류면_예외_없이_null이다()
    {
        var (provider, _) = Create(HttpStatusCode.InternalServerError, "server error");

        Assert.Null(await provider.GetOperatingStatusAsync("경복궁", 37.58, 126.98));
    }

    [Fact]
    public async Task 네트워크_예외도_던지지_않고_null이다()
    {
        var handler = new ThrowingHandler(new HttpRequestException("network down"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Google:PlacesApiKey"] = "test-api-key" })
            .Build();
        var provider = new GooglePlacesOperatingStatusProvider(
            new HttpClient(handler), configuration, NullLogger<GooglePlacesOperatingStatusProvider>.Instance);

        Assert.Null(await provider.GetOperatingStatusAsync("경복궁", 37.58, 126.98));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("YOUR_GOOGLE_PLACES_API_KEY")]
    public void 키가_없거나_플레이스홀더면_생성시점에_실패한다(string apiKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Google:PlacesApiKey"] = apiKey })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(
            () => new GooglePlacesOperatingStatusProvider(new HttpClient(), configuration, NullLogger<GooglePlacesOperatingStatusProvider>.Instance));
        Assert.Contains("Google:PlacesApiKey", ex.Message);
    }

    [Fact]
    public async Task 요청_헤더와_본문에_키_필드마스크_좌표바이어스가_들어간다()
    {
        var (provider, handler) = Create(HttpStatusCode.OK, PlacesResponse("OPERATIONAL"), apiKey: "my-secret-key");

        await provider.GetOperatingStatusAsync("경복궁", 37.58, 126.98);

        Assert.Equal("my-secret-key", handler.LastRequest!.Headers.GetValues("X-Goog-Api-Key").Single());
        Assert.Equal("places.businessStatus,places.displayName", handler.LastRequest.Headers.GetValues("X-Goog-FieldMask").Single());
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("https://places.googleapis.com/v1/places:searchText", handler.LastRequest.RequestUri!.ToString());

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("경복궁", body.RootElement.GetProperty("textQuery").GetString());
        Assert.Equal("ko", body.RootElement.GetProperty("languageCode").GetString());
        var center = body.RootElement.GetProperty("locationBias").GetProperty("circle").GetProperty("center");
        Assert.Equal(37.58, center.GetProperty("latitude").GetDouble());
        Assert.Equal(126.98, center.GetProperty("longitude").GetDouble());
    }
}
