using SharedData.Models;

namespace SharedData.DTOs
{
    public class ExpenseSplitDto
    {
        public int UserId { get; set; }

        public decimal ShareAmount { get; set; }
    }

    public class ExpenseUpsertDto
    {
        public int PaidByUserId { get; set; }
        public decimal Amount { get; set; }
        public ExpenseCategory Category { get; set; } = ExpenseCategory.ETC;
        public DateTime Date { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public string Memo { get; set; } = string.Empty;
        public int? ScheduleId { get; set; }

        // 균등분할 대상 멤버. Splits 가 비어 있을 때만 사용한다.
        public List<int> SplitMemberIds { get; set; } = new();

        // 커스텀 금액 분담. 값이 있으면 합계가 Amount 와 같아야 한다.
        public List<ExpenseSplitDto> Splits { get; set; } = new();
    }

    public class ExpenseResponseDto
    {
        public int Id { get; set; }
        public int TripId { get; set; }
        public int PaidByUserId { get; set; }
        public decimal Amount { get; set; }
        public ExpenseCategory Category { get; set; }
        public DateTime Date { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public string Memo { get; set; } = string.Empty;
        public int? ScheduleId { get; set; }
        public List<ExpenseSplitDto> Splits { get; set; } = new();
    }

    public class BudgetUpdateDto
    {
        public decimal? BudgetAmount { get; set; }
    }

    public class BudgetSummaryDto
    {
        public decimal? Budget { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal? Remaining { get; set; }
        public bool IsOverBudget { get; set; }
    }

    public class ExpenseBreakdownItemDto
    {
        public ExpenseCategory Category { get; set; }
        public decimal Total { get; set; }
        public decimal Ratio { get; set; }
    }

    public class SettlementTransferDto
    {
        public int FromUserId { get; set; }
        public int ToUserId { get; set; }
        public decimal Amount { get; set; }
    }

    // 확정(finalize)된 정산 스냅샷. Transfers는 확정 시점의 계산 결과를 그대로 저장한 것이라,
    // 확정 이후 지출이 추가/수정돼도 바뀌지 않는다.
    public class SettlementDto
    {
        public int Id { get; set; }
        public int TripId { get; set; }
        public int FinalizedByUserId { get; set; }
        public DateTime FinalizedAt { get; set; }
        public List<SettlementTransferDto> Transfers { get; set; } = new();
    }
}
