using System;

namespace SharedData.DTOs
{
    // AI가 추천하는 장소 하나. 대표 사진은 절대 포함하지 않으며,
    // 프론트에서 PlaceName으로 Google Places API를 별도 조회해 사진을 붙인다.
    //
    // PlaceId/Latitude/Longitude는 장소 검색 API(주변 카테고리 검색) 결과와 대조해
    // 좌표를 확정할 수 있었던 장소에만 채워진다 (PlaceRecommendationGrounder 참고).
    // 좌표를 확정하지 못한 추천은 컨트롤러 단계에서 걸러지므로, 클라이언트에 도달하는
    // 추천은 항상 PlaceId/Latitude/Longitude가 채워져 있다고 신뢰할 수 있다.
    public class AiPlaceRecommendationDto
    {
        public string PlaceName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime SuggestedStartTime { get; set; }

        public DateTime SuggestedEndTime { get; set; }

        public string? PlaceId { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }
}
