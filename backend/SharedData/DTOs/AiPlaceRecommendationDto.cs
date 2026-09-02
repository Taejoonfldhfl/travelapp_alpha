using System;

namespace SharedData.DTOs
{
    // AI가 추천하는 장소 하나. 대표 사진은 절대 포함하지 않으며,
    // 프론트에서 PlaceName으로 Google Places API를 별도 조회해 사진을 붙인다.
    public class AiPlaceRecommendationDto
    {
        public string PlaceName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime SuggestedStartTime { get; set; }

        public DateTime SuggestedEndTime { get; set; }
    }
}
