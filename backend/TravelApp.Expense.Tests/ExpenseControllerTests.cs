using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedData.DTOs;
using SharedData.Models;
using TravelApp.WebAPI.Controllers;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services.Expenses;
using SharedExpense = SharedData.Models.Expense;

namespace TravelApp.Expense.Tests;

public class ExpenseControllerTests
{
    private sealed class NoopPushNotifier : IPushNotifier
    {
        public Task NotifyTripMembersAsync(int tripId, string title, string body, int? excludeUserId = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    // 호출 여부/인자를 검증하기 위한 스파이.
    private sealed class SpyPushNotifier : IPushNotifier
    {
        public List<(int TripId, string Title, string Body, int? ExcludeUserId)> Calls { get; } = new();

        public Task NotifyTripMembersAsync(int tripId, string title, string body, int? excludeUserId = null, CancellationToken ct = default)
        {
            Calls.Add((tripId, title, body, excludeUserId));
            return Task.CompletedTask;
        }
    }

    private static (ExpenseController controller, ApplicationDbContext db) Create(
        int currentUserId = 1, IPushNotifier? pushNotifier = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);

        var controller = new ExpenseController(db, pushNotifier ?? new NoopPushNotifier())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()) }, "test"))
                }
            }
        };
        return (controller, db);
    }

    private static void SeedTrip(ApplicationDbContext db, decimal? budget)
    {
        db.Trips.Add(new Trip { Id = 1, Title = "t", OwnerId = 1, BudgetAmount = budget });
        db.TripMembers.Add(new TripMember { TripId = 1, UserId = 1 });
        db.TripMembers.Add(new TripMember { TripId = 1, UserId = 2 });
        db.SaveChanges();
    }

    private static void AddExpense(ApplicationDbContext db, ExpenseCategory category, decimal amount)
    {
        db.Expenses.Add(new SharedExpense { TripId = 1, PaidByUserId = 1, Amount = amount, Category = category });
        db.SaveChanges();
    }

    [Fact]
    public async Task BudgetSummary_UnderBudget_ReturnsRemaining()
    {
        var (controller, db) = Create();
        SeedTrip(db, 100000m);
        AddExpense(db, ExpenseCategory.FOOD, 30000m);

        var result = await controller.GetBudgetSummary(1);

        var dto = Assert.IsType<OkObjectResult>(result.Result).Value as BudgetSummaryDto;
        Assert.NotNull(dto);
        Assert.Equal(30000m, dto.TotalSpent);
        Assert.Equal(70000m, dto.Remaining);
        Assert.False(dto.IsOverBudget);
    }

    [Fact]
    public async Task BudgetSummary_OverBudget_FlagsOverBudget()
    {
        var (controller, db) = Create();
        SeedTrip(db, 50000m);
        AddExpense(db, ExpenseCategory.LODGING, 80000m);

        var result = await controller.GetBudgetSummary(1);

        var dto = (BudgetSummaryDto)Assert.IsType<OkObjectResult>(result.Result).Value!;
        Assert.True(dto.IsOverBudget);
        Assert.Equal(-30000m, dto.Remaining);
    }

    [Fact]
    public async Task BudgetSummary_NoBudgetSet_NeverOverBudget()
    {
        var (controller, db) = Create();
        SeedTrip(db, null);
        AddExpense(db, ExpenseCategory.ETC, 10000m);

        var result = await controller.GetBudgetSummary(1);

        var dto = (BudgetSummaryDto)Assert.IsType<OkObjectResult>(result.Result).Value!;
        Assert.Null(dto.Budget);
        Assert.Null(dto.Remaining);
        Assert.False(dto.IsOverBudget);
    }

    [Fact]
    public async Task BudgetSummary_NonMember_IsForbidden()
    {
        var (controller, db) = Create(currentUserId: 99);
        SeedTrip(db, 100000m);

        var result = await controller.GetBudgetSummary(1);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task ExpenseBreakdown_GroupsByCategoryWithRatio()
    {
        var (controller, db) = Create();
        SeedTrip(db, null);
        AddExpense(db, ExpenseCategory.FOOD, 10000m);
        AddExpense(db, ExpenseCategory.FOOD, 20000m);
        AddExpense(db, ExpenseCategory.TRANSPORT, 10000m);

        var result = await controller.GetExpenseBreakdown(1);

        var list = (List<ExpenseBreakdownItemDto>)Assert.IsType<OkObjectResult>(result.Result).Value!;
        Assert.Equal(2, list.Count);
        Assert.Equal(ExpenseCategory.FOOD, list[0].Category);
        Assert.Equal(30000m, list[0].Total);
        Assert.Equal(0.75m, list[0].Ratio);
    }

    [Fact]
    public async Task ExpenseBreakdown_UnknownTrip_ReturnsNotFound()
    {
        var (controller, _) = Create();

        var result = await controller.GetExpenseBreakdown(404);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateExpense_EqualSplit_GivesRemainderToPayer()
    {
        var (controller, db) = Create();
        SeedTrip(db, null);

        var result = await controller.CreateExpense(1, new ExpenseUpsertDto
        {
            PaidByUserId = 2,
            Amount = 10000.01m,
            Category = ExpenseCategory.FOOD,
            Date = DateTime.UtcNow,
            SplitMemberIds = new List<int> { 1, 2 }
        });

        var dto = (ExpenseResponseDto)Assert.IsType<OkObjectResult>(result.Result).Value!;
        Assert.Equal(10000.01m, dto.Splits.Sum(s => s.ShareAmount));
        Assert.Equal(5000m, dto.Splits.Single(s => s.UserId == 1).ShareAmount);
        Assert.Equal(5000.01m, dto.Splits.Single(s => s.UserId == 2).ShareAmount);
    }

    [Fact]
    public async Task CreateExpense_CustomSplitNotSummingToAmount_IsRejected()
    {
        var (controller, db) = Create();
        SeedTrip(db, null);

        var result = await controller.CreateExpense(1, new ExpenseUpsertDto
        {
            PaidByUserId = 1,
            Amount = 10000m,
            Splits = new List<ExpenseSplitDto>
            {
                new() { UserId = 1, ShareAmount = 3000m },
                new() { UserId = 2, ShareAmount = 3000m }
            }
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetExpenses_IncludesSplitsForEachExpense()
    {
        var (controller, db) = Create();
        SeedTrip(db, null);
        db.Expenses.Add(new SharedExpense
        {
            TripId = 1, PaidByUserId = 1, Amount = 30000m, Category = ExpenseCategory.FOOD,
            Date = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            Splits = new List<ExpenseSplit>
            {
                new() { UserId = 1, ShareAmount = 10000m },
                new() { UserId = 2, ShareAmount = 20000m }
            }
        });
        db.Expenses.Add(new SharedExpense
        {
            TripId = 1, PaidByUserId = 2, Amount = 5000m, Category = ExpenseCategory.ETC,
            Date = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
            Splits = new List<ExpenseSplit> { new() { UserId = 2, ShareAmount = 5000m } }
        });
        db.SaveChanges();

        var result = await controller.GetExpenses(1);

        var list = (List<ExpenseResponseDto>)Assert.IsType<OkObjectResult>(result.Result).Value!;
        Assert.Equal(2, list.Count);
        Assert.All(list, e => Assert.NotEmpty(e.Splits));
        var first = list.Single(e => e.Amount == 30000m);
        Assert.Equal(10000m, first.Splits.Single(s => s.UserId == 1).ShareAmount);
        Assert.Equal(20000m, first.Splits.Single(s => s.UserId == 2).ShareAmount);
        Assert.Equal(list.Single(e => e.Amount == 5000m).Splits.Single().UserId, 2);
    }

    // TODO: 수정/삭제 시 분담 교체, 예산 설정 권한(Owner 전용) 통합 테스트
    [Fact(Skip = "skeleton")]
    public Task UpdateExpense_ReplacesSplits() => Task.CompletedTask;

    // ---- 정산 확정(finalize) / 조회 ----

    private static void AddExpenseWithSplit(ApplicationDbContext db, int payerId, decimal amount, int shareUserId, decimal shareAmount)
    {
        db.Expenses.Add(new SharedExpense
        {
            TripId = 1,
            PaidByUserId = payerId,
            Amount = amount,
            Category = ExpenseCategory.FOOD,
            Splits = new List<ExpenseSplit> { new() { UserId = shareUserId, ShareAmount = shareAmount } }
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Settlement_ReturnsMinimizedTransfers()
    {
        var (controller, db) = Create();
        SeedTrip(db, null);
        // 1이 10000원을 내고 2가 그중 4000원을 분담 -> 2가 1에게 4000원을 줘야 함
        AddExpenseWithSplit(db, payerId: 1, amount: 10000m, shareUserId: 2, shareAmount: 4000m);

        var result = await controller.GetSettlement(1);

        var list = (List<SettlementTransferDto>)Assert.IsType<OkObjectResult>(result.Result).Value!;
        var transfer = Assert.Single(list);
        Assert.Equal(2, transfer.FromUserId);
        Assert.Equal(1, transfer.ToUserId);
        Assert.Equal(4000m, transfer.Amount);
    }

    [Fact]
    public async Task FinalizeSettlement_PersistsSnapshotAndNotifiesMembersExcludingFinalizer()
    {
        var spy = new SpyPushNotifier();
        var (controller, db) = Create(currentUserId: 1, pushNotifier: spy);
        SeedTrip(db, null);
        AddExpenseWithSplit(db, payerId: 1, amount: 10000m, shareUserId: 2, shareAmount: 4000m);

        var result = await controller.FinalizeSettlement(1);

        var dto = (SettlementDto)Assert.IsType<OkObjectResult>(result.Result).Value!;
        Assert.True(dto.Id > 0);
        Assert.Equal(1, dto.TripId);
        Assert.Equal(1, dto.FinalizedByUserId);
        var transfer = Assert.Single(dto.Transfers);
        Assert.Equal(2, transfer.FromUserId);
        Assert.Equal(1, transfer.ToUserId);
        Assert.Equal(4000m, transfer.Amount);

        Assert.Single(db.Settlements);
        var call = Assert.Single(spy.Calls);
        Assert.Equal(1, call.TripId);
        Assert.Equal(1, call.ExcludeUserId); // 확정한 본인은 제외
    }

    [Fact]
    public async Task FinalizeSettlement_NonMember_IsForbidden()
    {
        var (controller, db) = Create(currentUserId: 99);
        SeedTrip(db, null);

        var result = await controller.FinalizeSettlement(1);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetFinalizedSettlement_ReturnsPersistedSnapshotEvenAfterExpensesChange()
    {
        var (controller, db) = Create();
        SeedTrip(db, null);
        AddExpenseWithSplit(db, payerId: 1, amount: 10000m, shareUserId: 2, shareAmount: 4000m);
        var finalized = (SettlementDto)Assert.IsType<OkObjectResult>((await controller.FinalizeSettlement(1)).Result).Value!;

        // 확정 후 지출이 더 생겨도 이미 확정된 스냅샷은 바뀌지 않아야 한다.
        AddExpenseWithSplit(db, payerId: 2, amount: 6000m, shareUserId: 1, shareAmount: 3000m);

        var result = await controller.GetFinalizedSettlement(1, finalized.Id);

        var dto = (SettlementDto)Assert.IsType<OkObjectResult>(result.Result).Value!;
        var transfer = Assert.Single(dto.Transfers);
        Assert.Equal(4000m, transfer.Amount);
    }

    [Fact]
    public async Task GetFinalizedSettlement_NonMember_IsForbidden()
    {
        var (controller, db) = Create(currentUserId: 1);
        SeedTrip(db, null);
        var finalized = (SettlementDto)Assert.IsType<OkObjectResult>((await controller.FinalizeSettlement(1)).Result).Value!;

        // 같은 DB를, 여행 멤버가 아닌 사용자(99)로 다시 조회한다.
        var (outsiderController, _) = Create(currentUserId: 99);
        outsiderController = new ExpenseController(db, new NoopPushNotifier())
        {
            ControllerContext = outsiderController.ControllerContext
        };

        var outsiderResult = await outsiderController.GetFinalizedSettlement(1, finalized.Id);

        Assert.IsType<ForbidResult>(outsiderResult.Result);
    }

    [Fact]
    public async Task GetFinalizedSettlement_UnknownId_ReturnsNotFound()
    {
        var (controller, db) = Create();
        SeedTrip(db, null);

        var result = await controller.GetFinalizedSettlement(1, 404);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
