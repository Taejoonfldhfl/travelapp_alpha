using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedData.DTOs;
using SharedData.Models;
using System.Security.Claims;
using System.Text.Json;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services.Expenses;

namespace TravelApp.WebAPI.Controllers
{
    [Route("api")]
    [ApiController]
    [Authorize]
    public class ExpenseController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPushNotifier _pushNotifier;

        public ExpenseController(ApplicationDbContext context, IPushNotifier pushNotifier)
        {
            _context = context;
            _pushNotifier = pushNotifier;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private Task<bool> IsMemberAsync(int tripId, int userId)
            => _context.TripMembers.AnyAsync(m => m.TripId == tripId && m.UserId == userId);

        // 1. 지출 등록
        [HttpPost("Trip/{tripId}/expenses")]
        public async Task<ActionResult<ExpenseResponseDto>> CreateExpense(int tripId, ExpenseUpsertDto request)
        {
            if (!await IsMemberAsync(tripId, CurrentUserId)) return Forbid();

            var expense = new Expense { TripId = tripId };
            var error = await ApplyAsync(expense, request);
            if (error != null) return BadRequest(error);

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            await NotifyIfOverBudgetAsync(tripId);
            return Ok(ToDto(expense));
        }

        // 2. 트립별 지출 목록
        [HttpGet("Trip/{tripId}/expenses")]
        public async Task<ActionResult<List<ExpenseResponseDto>>> GetExpenses(int tripId)
        {
            if (!await IsMemberAsync(tripId, CurrentUserId)) return Forbid();

            var expenses = await _context.Expenses
                .Include(e => e.Splits)
                .Where(e => e.TripId == tripId)
                .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
                .ToListAsync();

            return Ok(expenses.Select(ToDto).ToList());
        }

        // 3. 지출 단건 조회
        [HttpGet("Expense/{id}")]
        public async Task<ActionResult<ExpenseResponseDto>> GetExpense(int id)
        {
            var expense = await _context.Expenses.Include(e => e.Splits).FirstOrDefaultAsync(e => e.Id == id);
            if (expense == null) return NotFound();
            if (!await IsMemberAsync(expense.TripId, CurrentUserId)) return Forbid();
            return Ok(ToDto(expense));
        }

        // 4. 지출 수정 (분담 내역은 통째로 교체)
        [HttpPut("Expense/{id}")]
        public async Task<ActionResult<ExpenseResponseDto>> UpdateExpense(int id, ExpenseUpsertDto request)
        {
            var expense = await _context.Expenses.Include(e => e.Splits).FirstOrDefaultAsync(e => e.Id == id);
            if (expense == null) return NotFound();
            if (!await IsMemberAsync(expense.TripId, CurrentUserId)) return Forbid();

            var error = await ApplyAsync(expense, request);
            if (error != null) return BadRequest(error);

            await _context.SaveChangesAsync();
            await NotifyIfOverBudgetAsync(expense.TripId);
            return Ok(ToDto(expense));
        }

        // 5. 지출 삭제
        [HttpDelete("Expense/{id}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id);
            if (expense == null) return NotFound();
            if (!await IsMemberAsync(expense.TripId, CurrentUserId)) return Forbid();

            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 6. 예산 설정 (null 이면 예산 해제)
        [HttpPut("Trip/{tripId}/budget")]
        public async Task<IActionResult> SetBudget(int tripId, BudgetUpdateDto request)
        {
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == tripId);
            if (trip == null) return NotFound();
            if (trip.OwnerId != CurrentUserId) return Forbid();
            if (request.BudgetAmount < 0) return BadRequest("예산은 0 이상이어야 합니다.");

            trip.BudgetAmount = request.BudgetAmount;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 7. 예산 대비 잔액
        [HttpGet("Trip/{tripId}/budget-summary")]
        public async Task<ActionResult<BudgetSummaryDto>> GetBudgetSummary(int tripId)
        {
            var trip = await _context.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId);
            if (trip == null) return NotFound();
            if (!await IsMemberAsync(tripId, CurrentUserId)) return Forbid();

            return Ok(await BuildBudgetSummaryAsync(trip));
        }

        // 8. 카테고리별 지출 합계 (GROUP BY category)
        [HttpGet("Trip/{tripId}/expense-breakdown")]
        public async Task<ActionResult<List<ExpenseBreakdownItemDto>>> GetExpenseBreakdown(int tripId)
        {
            if (!await _context.Trips.AnyAsync(t => t.Id == tripId)) return NotFound();
            if (!await IsMemberAsync(tripId, CurrentUserId)) return Forbid();

            var totals = await _context.Expenses
                .Where(e => e.TripId == tripId)
                .GroupBy(e => e.Category)
                .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount) })
                .ToListAsync();

            decimal grand = totals.Sum(t => t.Total);
            var result = totals
                .OrderByDescending(t => t.Total)
                .Select(t => new ExpenseBreakdownItemDto
                {
                    Category = t.Category,
                    Total = t.Total,
                    Ratio = grand == 0 ? 0 : Math.Round(t.Total / grand, 4)
                })
                .ToList();

            return Ok(result);
        }

        // 9. 더치페이 정산 결과 (실시간 계산 — 지출이 바뀌면 결과도 바뀐다. 확정본은 10/11번 참고)
        [HttpGet("Trip/{tripId}/settlement")]
        public async Task<ActionResult<List<SettlementTransferDto>>> GetSettlement(int tripId)
        {
            if (!await _context.Trips.AnyAsync(t => t.Id == tripId)) return NotFound();
            if (!await IsMemberAsync(tripId, CurrentUserId)) return Forbid();

            return Ok(await ComputeSettlementTransfersAsync(tripId));
        }

        // 10. 정산 확정 — 현재 정산 결과를 스냅샷으로 저장하고, 확정한 본인을 제외한 같은 여행
        // 멤버 전원에게 "정산 결과가 도착했어요" 알림을 보낸다.
        [HttpPost("Trip/{tripId}/Expense/settlements/finalize")]
        public async Task<ActionResult<SettlementDto>> FinalizeSettlement(int tripId)
        {
            var trip = await _context.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId);
            if (trip == null) return NotFound();
            if (!await IsMemberAsync(tripId, CurrentUserId)) return Forbid();

            var transfers = await ComputeSettlementTransfersAsync(tripId);

            var settlement = new Settlement
            {
                TripId = tripId,
                FinalizedByUserId = CurrentUserId,
                TransfersJson = JsonSerializer.Serialize(transfers)
            };
            _context.Settlements.Add(settlement);
            await _context.SaveChangesAsync();

            await _pushNotifier.NotifyTripMembersAsync(
                tripId, "정산 결과 도착", $"'{trip.Title}' 여행 정산 결과가 도착했어요.", excludeUserId: CurrentUserId);

            return Ok(ToSettlementDto(settlement, transfers));
        }

        // 11. 확정된 정산 결과 조회 — 본인이 속한 여행이 아니면 403.
        [HttpGet("Trip/{tripId}/Expense/settlements/{settlementId}")]
        public async Task<ActionResult<SettlementDto>> GetFinalizedSettlement(int tripId, int settlementId)
        {
            if (!await IsMemberAsync(tripId, CurrentUserId)) return Forbid();

            var settlement = await _context.Settlements
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == settlementId && s.TripId == tripId);
            if (settlement == null) return NotFound();

            var transfers = JsonSerializer.Deserialize<List<SettlementTransferDto>>(settlement.TransfersJson)
                ?? new List<SettlementTransferDto>();
            return Ok(ToSettlementDto(settlement, transfers));
        }

        // ---- helpers ----

        private async Task<List<SettlementTransferDto>> ComputeSettlementTransfersAsync(int tripId)
        {
            var expenses = await _context.Expenses
                .AsNoTracking()
                .Include(e => e.Splits)
                .Where(e => e.TripId == tripId)
                .ToListAsync();

            var entries = expenses.Select(e => new SettlementCalculator.ExpenseEntry(
                new[] { new SettlementCalculator.Payment(e.PaidByUserId, e.Amount) },
                e.Splits.Select(s => new SettlementCalculator.Share(s.UserId, s.ShareAmount)).ToList()));

            return SettlementCalculator.Settle(entries);
        }

        private static SettlementDto ToSettlementDto(Settlement settlement, List<SettlementTransferDto> transfers) => new()
        {
            Id = settlement.Id,
            TripId = settlement.TripId,
            FinalizedByUserId = settlement.FinalizedByUserId,
            FinalizedAt = settlement.FinalizedAt,
            Transfers = transfers
        };

        private async Task<BudgetSummaryDto> BuildBudgetSummaryAsync(Trip trip)
        {
            decimal spent = await _context.Expenses
                .Where(e => e.TripId == trip.Id)
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            return new BudgetSummaryDto
            {
                Budget = trip.BudgetAmount,
                TotalSpent = spent,
                Remaining = trip.BudgetAmount - spent,
                IsOverBudget = trip.BudgetAmount.HasValue && spent > trip.BudgetAmount.Value
            };
        }

        // 다른 멤버의 지출로 예산이 초과된 경우를 위한 서버 푸시 지점 (현재는 Mock 이 로그만 남김)
        private async Task NotifyIfOverBudgetAsync(int tripId)
        {
            var trip = await _context.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId);
            if (trip == null) return;
            var summary = await BuildBudgetSummaryAsync(trip);
            if (summary.IsOverBudget)
            {
                await _pushNotifier.NotifyTripMembersAsync(
                    tripId, "예산 초과", $"'{trip.Title}' 여행 예산을 {-summary.Remaining:N0}원 초과했습니다.");
            }
        }

        // 요청을 엔티티에 반영한다. 검증 실패 시 오류 메시지를 반환한다.
        private async Task<string?> ApplyAsync(Expense expense, ExpenseUpsertDto request)
        {
            if (request.Amount <= 0) return "금액은 0보다 커야 합니다.";
            if (!Enum.IsDefined(request.Category)) return "잘못된 카테고리입니다.";

            var memberIds = await _context.TripMembers
                .Where(m => m.TripId == expense.TripId)
                .Select(m => m.UserId)
                .ToListAsync();

            if (!memberIds.Contains(request.PaidByUserId)) return "결제자가 여행 멤버가 아닙니다.";

            if (request.ScheduleId.HasValue)
            {
                bool ok = await _context.Schedules.AnyAsync(s => s.Id == request.ScheduleId && s.TripId == expense.TripId);
                if (!ok) return "해당 여행의 일정이 아닙니다.";
            }

            List<ExpenseSplit> splits;
            if (request.Splits.Count > 0)
            {
                if (request.Splits.Any(s => s.ShareAmount < 0)) return "분담 금액은 0 이상이어야 합니다.";
                if (request.Splits.Select(s => s.UserId).Distinct().Count() != request.Splits.Count)
                    return "분담 멤버가 중복되었습니다.";
                if (request.Splits.Sum(s => s.ShareAmount) != request.Amount)
                    return "분담 금액의 합계가 지출 금액과 일치해야 합니다.";
                splits = request.Splits
                    .Select(s => new ExpenseSplit { UserId = s.UserId, ShareAmount = s.ShareAmount })
                    .ToList();
            }
            else
            {
                var ids = request.SplitMemberIds.Distinct().ToList();
                if (ids.Count == 0) return "분담 멤버를 한 명 이상 선택해야 합니다.";
                splits = SplitEqually(request.Amount, ids, request.PaidByUserId);
            }

            if (splits.Any(s => !memberIds.Contains(s.UserId))) return "분담 대상에 여행 멤버가 아닌 사용자가 있습니다.";

            expense.PaidByUserId = request.PaidByUserId;
            expense.Amount = request.Amount;
            expense.Category = request.Category;
            expense.Date = request.Date;
            expense.MerchantName = request.MerchantName;
            expense.Memo = request.Memo;
            expense.ScheduleId = request.ScheduleId;

            expense.Splits.Clear();
            expense.Splits.AddRange(splits);
            return null;
        }

        // 소수 둘째 자리까지 내림 균등분할, 남은 금액은 결제자(분담자에 포함된 경우) 또는 첫 번째 멤버가 부담한다.
        public static List<ExpenseSplit> SplitEqually(decimal amount, IReadOnlyList<int> userIds, int payerId)
        {
            decimal each = Math.Floor(amount / userIds.Count * 100m) / 100m;
            var splits = userIds.Select(id => new ExpenseSplit { UserId = id, ShareAmount = each }).ToList();
            decimal remainder = amount - each * userIds.Count;
            var target = splits.FirstOrDefault(s => s.UserId == payerId) ?? splits[0];
            target.ShareAmount += remainder;
            return splits;
        }

        private static ExpenseResponseDto ToDto(Expense e) => new()
        {
            Id = e.Id,
            TripId = e.TripId,
            PaidByUserId = e.PaidByUserId,
            Amount = e.Amount,
            Category = e.Category,
            Date = e.Date,
            MerchantName = e.MerchantName,
            Memo = e.Memo,
            ScheduleId = e.ScheduleId,
            Splits = e.Splits.Select(s => new ExpenseSplitDto { UserId = s.UserId, ShareAmount = s.ShareAmount }).ToList()
        };
    }
}
