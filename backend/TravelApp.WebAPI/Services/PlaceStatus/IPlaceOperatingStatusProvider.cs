using SharedData.DTOs;

namespace TravelApp.WebAPI.Services.PlaceStatus
{
    // 장소 검색 provider(Tmap 등)가 영업상태를 못 주는 경우(OperatingStatus=Unknown)를 보완하는 보조 신호.
    // 확인하지 못하면(장소를 찾지 못함, API 실패 등) null을 돌려주고, 호출한 쪽은 이 경우 기존 상태를
    // 그대로 유지해야 한다 — 추천 그라운딩의 필수 경로가 아니라 보조 신호이기 때문이다.
    public interface IPlaceOperatingStatusProvider
    {
        Task<PlaceOperatingStatusResult?> GetOperatingStatusAsync(
            string placeName,
            double latitude,
            double longitude,
            CancellationToken cancellationToken = default);
    }

    // CheckedAtUtc는 이 확인이 이뤄진 시점이다(PlaceSearchResultDto.LastConfirmedOperatingDate에 그대로 들어가
    // PlaceRecommendationGrounder.IsOperatingAsOf의 정보 신선도 판단에 쓰인다).
    public record PlaceOperatingStatusResult(
        PlaceOperatingStatus Status,
        bool IsPermanentlyClosed,
        DateTime CheckedAtUtc);
}
