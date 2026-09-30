using Microsoft.Extensions.Logging.Abstractions;
using SharedData.DTOs;
using TravelApp.WebAPI.Services.PlaceSearch;
using TravelApp.WebAPI.Services.PlaceStatus;

namespace TravelApp.WebAPI.Tests;

public class StatusEnrichingNearbyPlaceSearchProviderTests
{
    // 검색어(장소 이름)별로 돌려줄 결과(또는 던질 예외)를 지정하는 가짜 statusProvider.
    private sealed class FakeStatusProvider : IPlaceOperatingStatusProvider
    {
        public Dictionary<string, PlaceOperatingStatusResult?> ResultsByName { get; } = new();
        public Dictionary<string, Exception> ThrowByName { get; } = new();
        public List<string> Queried { get; } = new();

        public Task<PlaceOperatingStatusResult?> GetOperatingStatusAsync(
            string placeName, double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            Queried.Add(placeName);

            if (ThrowByName.TryGetValue(placeName, out var ex))
            {
                throw ex;
            }

            return Task.FromResult(ResultsByName.GetValueOrDefault(placeName));
        }
    }

    private static PlaceSearchResultDto Place(string name, PlaceOperatingStatus status) => new()
    {
        PlaceId = $"id:{name}",
        Name = name,
        Latitude = 37.5,
        Longitude = 127.0,
        OperatingStatus = status
    };

    [Fact]
    public async Task Unknown인_결과만_보완하고_이미_상태가_있는_결과는_건드리지_않는다()
    {
        var inner = new FakeNameSearchProvider(new());
        inner.NearbyResults = [Place("휴관중인곳", PlaceOperatingStatus.Unknown), Place("영업중인곳", PlaceOperatingStatus.Open)];

        var statusProvider = new FakeStatusProvider();
        statusProvider.ResultsByName["휴관중인곳"] = new PlaceOperatingStatusResult(PlaceOperatingStatus.Closed, false, DateTime.UtcNow);
        // "영업중인곳"은 이미 Unknown이 아니므로 statusProvider가 호출되지 않아야 한다. 혹시 호출되면 테스트가 실패하도록 예외를 걸어둔다.
        statusProvider.ThrowByName["영업중인곳"] = new InvalidOperationException("Unknown이 아닌 결과는 조회하면 안 됨");

        var sut = new StatusEnrichingNearbyPlaceSearchProvider(inner, statusProvider, NullLogger<StatusEnrichingNearbyPlaceSearchProvider>.Instance);

        var results = await sut.SearchNearbyAsync(new NearbyPlaceSearchRequest(37.5, 127.0, 1000, null));

        Assert.Equal(["휴관중인곳"], statusProvider.Queried);
        Assert.Equal(PlaceOperatingStatus.Closed, results.Single(p => p.Name == "휴관중인곳").OperatingStatus);
        Assert.Equal(PlaceOperatingStatus.Open, results.Single(p => p.Name == "영업중인곳").OperatingStatus);
    }

    [Fact]
    public async Task statusProvider가_null을_돌려주면_Unknown을_유지한다()
    {
        var inner = new FakeNameSearchProvider(new());
        inner.NearbyResults = [Place("확인불가", PlaceOperatingStatus.Unknown)];

        var statusProvider = new FakeStatusProvider(); // ResultsByName에 없으면 null

        var sut = new StatusEnrichingNearbyPlaceSearchProvider(inner, statusProvider, NullLogger<StatusEnrichingNearbyPlaceSearchProvider>.Instance);
        var results = await sut.SearchNearbyAsync(new NearbyPlaceSearchRequest(37.5, 127.0, 1000, null));

        Assert.Equal(PlaceOperatingStatus.Unknown, results.Single().OperatingStatus);
    }

    [Fact]
    public async Task statusProvider가_예외를_던지면_그_장소는_Unknown을_유지하고_나머지는_계속_처리한다()
    {
        var inner = new FakeNameSearchProvider(new());
        inner.NearbyResults =
        [
            Place("장애난곳", PlaceOperatingStatus.Unknown),
            Place("정상확인됨", PlaceOperatingStatus.Unknown)
        ];

        var statusProvider = new FakeStatusProvider();
        statusProvider.ThrowByName["장애난곳"] = new InvalidOperationException("API 장애");
        statusProvider.ResultsByName["정상확인됨"] = new PlaceOperatingStatusResult(PlaceOperatingStatus.Open, false, DateTime.UtcNow);

        var sut = new StatusEnrichingNearbyPlaceSearchProvider(inner, statusProvider, NullLogger<StatusEnrichingNearbyPlaceSearchProvider>.Instance);
        var results = await sut.SearchNearbyAsync(new NearbyPlaceSearchRequest(37.5, 127.0, 1000, null));

        Assert.Equal(PlaceOperatingStatus.Unknown, results.Single(p => p.Name == "장애난곳").OperatingStatus);
        Assert.Equal(PlaceOperatingStatus.Open, results.Single(p => p.Name == "정상확인됨").OperatingStatus);
    }

    [Fact]
    public async Task SearchByNameAsync도_동일하게_보완한다()
    {
        var inner = new FakeNameSearchProvider(new()
        {
            ["몽로"] = [Place("몽로", PlaceOperatingStatus.Unknown)]
        });

        var statusProvider = new FakeStatusProvider();
        statusProvider.ResultsByName["몽로"] = new PlaceOperatingStatusResult(PlaceOperatingStatus.Closed, true, DateTime.UtcNow);

        var sut = new StatusEnrichingNearbyPlaceSearchProvider(inner, statusProvider, NullLogger<StatusEnrichingNearbyPlaceSearchProvider>.Instance);
        var results = await sut.SearchByNameAsync("몽로");

        var place = Assert.Single(results);
        Assert.Equal(PlaceOperatingStatus.Closed, place.OperatingStatus);
        Assert.True(place.IsPermanentlyClosed);
    }
}
