namespace TravelApp.WebAPI.Services.RouteOptimization.TspSolvers
{
    // 비트마스크 DP(Held-Karp)로 정확한 최적해를 구한다. 시작점/끝점을 고정하지 않는
    // "열린 경로(open path)" 형태의 TSP: 등록된 지점을 전부, 정확히 한 번씩 방문하되
    // 출발지로 되돌아오지 않는다 (여행 하루 일정처럼 출발지=숙소로 복귀할 필요가 없다고 가정).
    //
    // 시간복잡도 O(N^2 * 2^N)이라 N<=10 정도까지만 실용적이다.
    public static class HeldKarpSolver
    {
        public static TspSolution Solve(double[,] travelTimeSeconds)
        {
            int n = travelTimeSeconds.GetLength(0);

            if (n == 1)
            {
                return new TspSolution(new[] { 0 }, 0);
            }

            int fullMask = (1 << n) - 1;

            // dp[mask, last] = mask로 표현된 지점 집합을 전부 방문하고 last에서 끝났을 때의 최소 이동시간.
            var dp = new double[1 << n, n];
            var parent = new int[1 << n, n];

            for (int mask = 0; mask < (1 << n); mask++)
            {
                for (int last = 0; last < n; last++)
                {
                    dp[mask, last] = double.PositiveInfinity;
                    parent[mask, last] = -1;
                }
            }

            // 시작점이 자유이므로 모든 단일 지점 집합이 기저 상태가 된다.
            for (int i = 0; i < n; i++)
            {
                dp[1 << i, i] = 0;
            }

            for (int mask = 1; mask <= fullMask; mask++)
            {
                for (int last = 0; last < n; last++)
                {
                    if ((mask & (1 << last)) == 0 || double.IsPositiveInfinity(dp[mask, last]))
                    {
                        continue;
                    }

                    for (int next = 0; next < n; next++)
                    {
                        if ((mask & (1 << next)) != 0)
                        {
                            continue;
                        }

                        int nextMask = mask | (1 << next);
                        double candidate = dp[mask, last] + travelTimeSeconds[last, next];

                        if (candidate < dp[nextMask, next])
                        {
                            dp[nextMask, next] = candidate;
                            parent[nextMask, next] = last;
                        }
                    }
                }
            }

            double bestCost = double.PositiveInfinity;
            int bestLast = -1;

            for (int last = 0; last < n; last++)
            {
                if (dp[fullMask, last] < bestCost)
                {
                    bestCost = dp[fullMask, last];
                    bestLast = last;
                }
            }

            var order = new List<int>();
            int mask2 = fullMask;
            int current = bestLast;

            while (current != -1)
            {
                order.Add(current);
                int prev = parent[mask2, current];
                mask2 &= ~(1 << current);
                current = prev;
            }

            order.Reverse();

            return new TspSolution(order.ToArray(), bestCost);
        }
    }
}
