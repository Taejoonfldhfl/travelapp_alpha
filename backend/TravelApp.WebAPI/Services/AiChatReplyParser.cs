using System.Text.Json;
using SharedData.DTOs;

namespace TravelApp.WebAPI.Services
{
    // LLM이 돌려준 JSON을 해석해 클라이언트 응답 DTO로 옮긴다. 새 스키마(placeId/name/reason)를 쓰되,
    // 예전 필드명(placeName/description)으로 답해도 받아들인다. 클라이언트 계약(AiChatResponseDto)은 바꾸지 않는다.
    public static class AiChatReplyParser
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // 해석 실패(JSON이 아니거나 형식이 다름)면 null.
        public static LlmChatReply? TryParse(string rawReply)
        {
            try
            {
                return JsonSerializer.Deserialize<LlmChatReply>(ExtractJson(rawReply), JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // 추천 시간이 빠져 있으면 여행 시작 시각을 기본값으로 쓴다(클라이언트가 시간 필드를 필수로 받기 때문).
        public static List<AiPlaceRecommendationDto> ToRecommendations(LlmChatReply reply, DateTime fallbackStart)
        {
            return (reply.Recommendations ?? [])
                .Select(r => new AiPlaceRecommendationDto
                {
                    PlaceId = string.IsNullOrWhiteSpace(r.PlaceId) ? null : r.PlaceId.Trim(),
                    PlaceName = (r.Name ?? r.PlaceName ?? string.Empty).Trim(),
                    Description = r.Reason ?? r.Description ?? string.Empty,
                    SuggestedStartTime = r.SuggestedStartTime ?? fallbackStart,
                    SuggestedEndTime = r.SuggestedEndTime ?? (r.SuggestedStartTime ?? fallbackStart).AddHours(1)
                })
                .ToList();
        }

        // 모델이 지시를 어기고 코드블록으로 감싸는 경우를 대비한 방어적 처리.
        private static string ExtractJson(string rawReply)
        {
            var text = rawReply.Trim();

            if (text.StartsWith("```"))
            {
                var firstNewline = text.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    text = text[(firstNewline + 1)..];
                }

                var closingFence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (closingFence >= 0)
                {
                    text = text[..closingFence];
                }
            }

            return text.Trim();
        }
    }

    public sealed class LlmChatReply
    {
        public string ReplyText { get; set; } = string.Empty;

        // LLM이 스스로 판단한 결과 상태("success" | "insufficient" | "not_recommendation"). 참고용이며 최종 상태는 서버가 정한다.
        public string? SearchStatus { get; set; }

        public List<LlmRecommendation>? Recommendations { get; set; }
    }

    public sealed class LlmRecommendation
    {
        public string? PlaceId { get; set; }
        public string? Name { get; set; }
        public string? Reason { get; set; }

        // 예전 스키마 필드명.
        public string? PlaceName { get; set; }
        public string? Description { get; set; }

        public DateTime? SuggestedStartTime { get; set; }
        public DateTime? SuggestedEndTime { get; set; }
    }
}
