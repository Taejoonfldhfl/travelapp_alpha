using System;

namespace SharedData.Models
{
    // 정산 확정 스냅샷. 확정 시점의 SettlementCalculator 결과를 저장해 두어,
    // 이후 지출이 추가/수정돼도 이미 공유한 정산 내용은 바뀌지 않게 한다.
    public class Settlement
    {
        public int Id { get; set; }

        public int TripId { get; set; }
        public Trip? Trip { get; set; }

        public int FinalizedByUserId { get; set; }
        public User? FinalizedByUser { get; set; }

        public DateTime FinalizedAt { get; set; } = DateTime.UtcNow;

        // [{ "fromUserId":.., "toUserId":.., "amount":.. }] 형태의 JSON.
        public string TransfersJson { get; set; } = "[]";
    }
}
