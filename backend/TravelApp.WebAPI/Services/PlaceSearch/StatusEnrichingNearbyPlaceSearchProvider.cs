using SharedData.DTOs;
using TravelApp.WebAPI.Services.PlaceStatus;

namespace TravelApp.WebAPI.Services.PlaceSearch
{
    // inner provider(Tmap/Mock)의 검색 결과 중 영업상태를 모르는(OperatingStatus=Unknown) 장소만
    // IPlaceOperatingStatusProvider(Google Places 등)로 보완하는 데코레이터. 이미 Unknown이 아닌 결과는
    // 건드리지 않는다 — inner provider가 이미 준 정보가 있으면 그게 우선이다.
    // statusProvider는 보조 신호일 뿐이라, 한 장소 조회 중 예외가 나도 그 장소만 Unknown으로 남기고
    // 나머지 장소는 계속 처리한다(한 곳 실패가 전체 검색 결과를 막지 않는다).
    public class StatusEnrichingNearbyPlaceSearchProvider : INearbyPlaceSearchProvider
    {
        private readonly INearbyPlaceSearchProvider _inner;
        private readonly IPlaceOperatingStatusProvider _statusProvider;
        private readonly ILogger<StatusEnrichingNearbyPlaceSearchProvider> _logger;

        public StatusEnrichingNearbyPlaceSearchProvider(
            INearbyPlaceSearchProvider inner,
            IPlaceOperatingStatusProvider statusProvider,
            ILogger<StatusEnrichingNearbyPlaceSearchProvider> logger)
        {
            _inner = inner;
            _statusProvider = statusProvider;
            _logger = logger;
        }

        public async Task<List<PlaceSearchResultDto>> SearchNearbyAsync(
            NearbyPlaceSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            var results = await _inner.SearchNearbyAsync(request, cancellationToken);
            await EnrichUnknownStatusesAsync(results, cancellationToken);
            return results;
        }

        public async Task<IReadOnlyList<PlaceSearchResultDto>> SearchByNameAsync(
            string placeName,
            CancellationToken cancellationToken = default)
        {
            var results = await _inner.SearchByNameAsync(placeName, cancellationToken);
            await EnrichUnknownStatusesAsync(results, cancellationToken);
            return results;
        }

        private async Task EnrichUnknownStatusesAsync(
            IReadOnlyList<PlaceSearchResultDto> results, CancellationToken cancellationToken)
        {
            foreach (var place in results)
            {
                if (place.OperatingStatus != PlaceOperatingStatus.Unknown)
                {
                    continue;
                }

                PlaceOperatingStatusResult? status;
                try
                {
                    status = await _statusProvider.GetOperatingStatusAsync(
                        place.Name, place.Latitude, place.Longitude, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "장소 영업상태 보완 중 오류가 발생했습니다 (place={PlaceName})", place.Name);
                    continue;
                }

                if (status == null)
                {
                    continue;
                }

                place.OperatingStatus = status.Status;
                place.IsPermanentlyClosed = status.IsPermanentlyClosed;
                place.LastConfirmedOperatingDate = status.CheckedAtUtc;
            }
        }
    }
}
