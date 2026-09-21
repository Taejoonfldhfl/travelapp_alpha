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
        public Task NotifyTripMembersAsync(int tripId, string title, string body, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private static (ExpenseController controller, ApplicationDbContext db) Create(int currentUserId = 1)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);

        var controller = new ExpenseController(db, new NoopPushNotifier())
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

    // TODO: 수정/삭제 시 분담 교체, 예산 설정 권한(Owner 전용), settlement 엔드포인트 통합 테스트
    [Fact(Skip = "skeleton")]
    public Task UpdateExpense_ReplacesSplits() => Task.CompletedTask;

    [Fact(Skip = "skeleton")]
    public Task Settlement_ReturnsMinimizedTransfers() => Task.CompletedTask;
}
