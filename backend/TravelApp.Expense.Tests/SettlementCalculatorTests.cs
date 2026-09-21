using SharedData.DTOs;
using TravelApp.WebAPI.Services.Expenses;
using static TravelApp.WebAPI.Services.Expenses.SettlementCalculator;

namespace TravelApp.Expense.Tests;

public class SettlementCalculatorTests
{
    private static ExpenseEntry Expense(int payer, decimal amount, params int[] sharedBy)
    {
        decimal each = amount / sharedBy.Length;
        return new ExpenseEntry(
            new[] { new Payment(payer, amount) },
            sharedBy.Select(u => new Share(u, each)).ToList());
    }

    // 결과를 적용했을 때 모든 사람의 순잔액이 0이 되는지 검증
    private static void AssertFullySettled(IReadOnlyDictionary<int, decimal> net, List<SettlementTransferDto> transfers)
    {
        var remaining = new Dictionary<int, decimal>(net);
        foreach (var t in transfers)
        {
            Assert.True(t.Amount > 0);
            remaining[t.FromUserId] = remaining.GetValueOrDefault(t.FromUserId) + t.Amount;
            remaining[t.ToUserId] = remaining.GetValueOrDefault(t.ToUserId) - t.Amount;
        }
        Assert.All(remaining.Values, v => Assert.Equal(0m, v));
    }

    [Fact]
    public void ThreePeople_OnePayerForEveryone_TwoTransfers()
    {
        // 1번이 30000원을 내고 1,2,3 이 균등 부담 → 2,3 이 1번에게 각 10000원
        var expenses = new[] { Expense(1, 30000m, 1, 2, 3) };

        var net = ComputeNetBalances(expenses);
        var result = Settle(net);

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(1, t.ToUserId));
        Assert.All(result, t => Assert.Equal(10000m, t.Amount));
        AssertFullySettled(net, result);
    }

    [Fact]
    public void FourPeople_CrossPayments_MinimizesTransfers()
    {
        // 서로 얽힌 지출: naive 하게 지출별로 정산하면 여러 건이지만 순잔액 기준으로는 줄어든다.
        var expenses = new[]
        {
            Expense(1, 40000m, 1, 2, 3, 4), // 식사: 1이 결제
            Expense(2, 20000m, 1, 2, 3, 4), // 택시: 2가 결제
            Expense(3, 60000m, 3, 4),       // 숙소 일부: 3이 결제, 3·4 부담
        };
        // net: 1=+40000-10000-5000=25000 / 2=+20000-10000-5000=5000 / 3=+60000-10000-5000-30000=15000 / 4=-10000-5000-30000=-45000
        var net = ComputeNetBalances(expenses);
        var result = Settle(net);

        Assert.Equal(25000m, net[1]);
        Assert.Equal(-45000m, net[4]);
        // 채무자 1명 → 채권자 3명: 참여자 수 - 1 = 3건이 최소
        Assert.Equal(3, result.Count);
        Assert.All(result, t => Assert.Equal(4, t.FromUserId));
        AssertFullySettled(net, result);
    }

    [Fact]
    public void FourPeople_TwoPairs_SettledInTwoTransfers_NotThree()
    {
        // 두 쌍이 서로 독립적으로 상계되는 경우: 그리디는 (n-1)건 이하를 보장하고 이 케이스는 2건으로 끝난다.
        var net = new Dictionary<int, decimal>
        {
            [1] = 10000m, [2] = -10000m,
            [3] = 5000m, [4] = -5000m
        };

        var result = Settle(net);

        Assert.Equal(2, result.Count);
        AssertFullySettled(net, result);
    }

    [Fact]
    public void TransferCount_NeverExceedsParticipantsMinusOne()
    {
        var net = new Dictionary<int, decimal>
        {
            [1] = 7000m, [2] = 3000m, [3] = -4000m, [4] = -6000m
        };

        var result = Settle(net);

        Assert.True(result.Count <= net.Count - 1);
        AssertFullySettled(net, result);
    }

    [Fact]
    public void AlreadyBalanced_ReturnsEmpty()
    {
        var expenses = new[] { Expense(1, 10000m, 1) };
        Assert.Empty(Settle(expenses));
    }
}
