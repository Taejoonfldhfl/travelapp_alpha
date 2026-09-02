using System;

namespace SharedData.Models
{
    // AI 장소추천 챗봇의 대화 세션. 여행(Trip) 하나에 여러 세션이 있을 수 있다.
    public class ChatSession
    {
        public int Id { get; set; }

        public int TripId { get; set; }
        public Trip? Trip { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        // 대화에서 추출된 슬롯(선호 지역/스타일 등)을 저장하기 위한 JSON. 현재는 빈 객체로 시작하는 예약 필드다.
        public string SlotsJson { get; set; } = "{}";

        // 최근 대화 히스토리(슬라이딩 윈도우, 최대 16개)를 [{ "role": ..., "content": ... }] 형태의 JSON으로 저장.
        public string MessagesJson { get; set; } = "[]";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
