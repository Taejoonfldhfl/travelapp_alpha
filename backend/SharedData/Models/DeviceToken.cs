using System;

namespace SharedData.Models
{
    // 사용자별 FCM(Firebase Cloud Messaging) 디바이스 토큰. 사용자당 최신 토큰 1개만 유지하며,
    // 토큰이 갱신되면(재설치, 앱 데이터 삭제 등) 기존 값을 덮어쓴다.
    public class DeviceToken
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public string Token { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
