using SharedData.DTOs;

namespace TravelApp.WebAPI.Services.PlaceSearch
{
    // 좌표 중심점 주변의 "실제 장소" 목록을 좌표(위도/경도)와 함께 가져오는 provider.
    // AI 챗봇의 장소 추천은 반드시 이 provider가 반환한 목록 안에서만 이뤄져야 한다 (규칙 2).
    // 구현체를 교체해도(Mock -> Tmap 주변 카테고리 검색 등) 상위 로직(AiChatController)은 그대로 재사용된다.
    public interface INearbyPlaceSearchProvider
    {
        Task<List<PlaceSearchResultDto>> SearchNearbyAsync(
            NearbyPlaceSearchRequest request,
            CancellationToken cancellationToken = default);

        // 사용자가 특정 장소(예: "경복궁", "강남역")를 기준으로 추천을 요청했을 때 쓴다.
        // 중심 좌표 없이 이름만으로 검색하므로 사용자의 현재 위치와 무관하게 동작하며,
        // 실제로 존재하는 장소인지/좌표를 이 호출로 검증한다. 못 찾으면 null —
        // 호출한 쪽(AiChatController)은 이 경우 해당 추천을 버려야 한다(규칙 2, 환각 방지).
        Task<PlaceSearchResultDto?> SearchByNameAsync(
            string placeName,
            CancellationToken cancellationToken = default);
    }
}
