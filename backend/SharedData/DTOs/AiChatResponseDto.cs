using System.Collections.Generic;

namespace SharedData.DTOs
{
    public class AiChatResponseDto
    {
        public string ReplyText { get; set; } = string.Empty;

        public List<AiPlaceRecommendationDto> Recommendations { get; set; } = new();
    }
}
