using System;

namespace SharedData.DTOs
{
    // AI가 추천하는 장소 하나. 대표 사진은 LLM 응답에서 받지 않는다 — ImageUrl은 검증을 통과한 추천에 대해서만
    // 서버의 IPlaceImageProvider가 채우며, null이면 프론트가 자체 조회하거나 "사진 없음"을 보여준다.
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

        // 검증에 쓴 후보의 영업 상태. 확인된 적 없으면 Unknown — 클라이언트는 이를 "영업 중"으로 표시하면 안 된다.
        public PlaceOperatingStatus OperatingStatus { get; set; } = PlaceOperatingStatus.Unknown;

        // 장소 대표 사진 URL(IPlaceImageProvider). 찾지 못했으면 null.
        public string? ImageUrl { get; set; }
    }
}
