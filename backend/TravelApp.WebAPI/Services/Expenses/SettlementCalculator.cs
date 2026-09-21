using SharedData.DTOs;

namespace TravelApp.WebAPI.Services.Expenses;

// 지출 목록으로부터 "누가 누구에게 얼마"를 계산하는 순수 로직 (DB 의존 없음).
// 순잔액(낸 돈 - 부담할 돈)을 구한 뒤 가장 큰 채무자와 가장 큰 채권자를 반복해서 짝지어
// 거래 건수를 줄인다. 각 거래마다 최소 한 명의 잔액이 0이 되므로 건수는 최대 (참여자 수 - 1).
public static class SettlementCalculator
{
    public readonly record struct Payment(int UserId, decimal Amount);
    public readonly record struct Share(int UserId, decimal Amount);
    public readonly record struct ExpenseEntry(IReadOnlyList<Payment> Payers, IReadOnlyList<Share> Shares);

    public static Dictionary<int, decimal> ComputeNetBalances(IEnumerable<ExpenseEntry> expenses)
    {
        var net = new Dictionary<int, decimal>();
        foreach (var e in expenses)
        {
            foreach (var p in e.Payers)
                net[p.UserId] = net.GetValueOrDefault(p.UserId) + p.Amount;
            foreach (var s in e.Shares)
                net[s.UserId] = net.GetValueOrDefault(s.UserId) - s.Amount;
        }
        return net;
    }

    public static List<SettlementTransferDto> Settle(IReadOnlyDictionary<int, decimal> netBalances)
    {
        // 양수 = 받을 돈(채권자), 음수 = 줄 돈(채무자)
        var creditors = netBalances.Where(kv => kv.Value > 0)
            .Select(kv => (Id: kv.Key, Amt: kv.Value)).ToList();
        var debtors = netBalances.Where(kv => kv.Value < 0)
            .Select(kv => (Id: kv.Key, Amt: -kv.Value)).ToList();

        var result = new List<SettlementTransferDto>();

        while (creditors.Count > 0 && debtors.Count > 0)
        {
            int ci = IndexOfMax(creditors);
            int di = IndexOfMax(debtors);
            var c = creditors[ci];
            var d = debtors[di];

            decimal amount = Math.Min(c.Amt, d.Amt);
            result.Add(new SettlementTransferDto
            {
                FromUserId = d.Id,
                ToUserId = c.Id,
                Amount = amount
            });

            if (c.Amt == amount) creditors.RemoveAt(ci); else creditors[ci] = (c.Id, c.Amt - amount);
            if (d.Amt == amount) debtors.RemoveAt(di); else debtors[di] = (d.Id, d.Amt - amount);
        }

        return result;
    }

    public static List<SettlementTransferDto> Settle(IEnumerable<ExpenseEntry> expenses)
        => Settle(ComputeNetBalances(expenses));

    private static int IndexOfMax(List<(int Id, decimal Amt)> list)
    {
        int best = 0;
        for (int i = 1; i < list.Count; i++)
            if (list[i].Amt > list[best].Amt) best = i;
        return best;
    }
}
