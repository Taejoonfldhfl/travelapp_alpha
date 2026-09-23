using System.Collections.Generic;

namespace SharedData.DTOs
{
    public class AiChatResponseDto
    {
        public string ReplyText { get; set; } = string.Empty;

        public List<AiPlaceRecommendationDto> Recommendations { get; set; } = new();

        // 아래 두 필드는 기존 클라이언트와의 호환을 위해 추가만 했다(기존 필드 의미는 그대로).
        // 검색/추천이 어떤 상태로 끝났는지 — 검색 결과 없음과 검색 서비스 오류를 구분한다.
        public ChatSearchStatus SearchStatus { get; set; } = ChatSearchStatus.Success;

        // 실제로 검색 기준으로 쓴 위치의 이름(예: "강남역", "현재 위치"). 기준점을 정하지 못했으면 null.
        public string? SearchLocation { get; set; }
    }
}
