namespace SharedData.Models
{
    public class Expense
    {
        public int Id { get; set; }

        public int TripId { get; set; }
        public Trip? Trip { get; set; }

        // 실제로 결제한 멤버
        public int PaidByUserId { get; set; }
        public User? PaidByUser { get; set; }

        public decimal Amount { get; set; }

        public ExpenseCategory Category { get; set; } = ExpenseCategory.ETC;

        public DateTime Date { get; set; }

        public string MerchantName { get; set; } = string.Empty;

        public string Memo { get; set; } = string.Empty;

        // 연관 일정 (선택). 일정이 삭제되면 null 로 남는다.
        public int? ScheduleId { get; set; }
        public Schedule? Schedule { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 분담 내역. 합계는 Amount 와 같아야 한다.
        public List<ExpenseSplit> Splits { get; set; } = new();
    }
}
