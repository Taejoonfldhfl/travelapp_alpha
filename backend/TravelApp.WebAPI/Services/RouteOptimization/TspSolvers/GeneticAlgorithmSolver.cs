namespace TravelApp.WebAPI.Services.RouteOptimization.TspSolvers
{
    // OR-Tools가 시간예산 안에 해를 못 찾았을 때 쓰는 폴백: 순열 기반 GA + 2-opt 지역 개선.
    // Held-Karp와 동일하게 "열린 경로"(출발지 복귀 없음)를 최소화한다.
    public static class GeneticAlgorithmSolver
    {
        private const int PopulationSize = 80;
        private const int Generations = 300;
        private const double MutationRate = 0.15;
        private const double ElitismRatio = 0.1;

        public static TspSolution Solve(double[,] travelTimeSeconds, int? randomSeed = null)
        {
            int n = travelTimeSeconds.GetLength(0);

            if (n <= 2)
            {
                var order = Enumerable.Range(0, n).ToArray();
                return new TspSolution(order, RouteCost(order, travelTimeSeconds));
            }

            var random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();

            var population = new List<int[]>(PopulationSize);
            for (int i = 0; i < PopulationSize; i++)
            {
                population.Add(RandomPermutation(n, random));
            }

            int eliteCount = Math.Max(1, (int)(PopulationSize * ElitismRatio));

            int[] bestOrder = population[0];
            double bestCost = RouteCost(bestOrder, travelTimeSeconds);

            for (int generation = 0; generation < Generations; generation++)
            {
                var ranked = population
                    .Select(order => (Order: order, Cost: RouteCost(order, travelTimeSeconds)))
                    .OrderBy(x => x.Cost)
                    .ToList();

                if (ranked[0].Cost < bestCost)
                {
                    bestCost = ranked[0].Cost;
                    bestOrder = ranked[0].Order;
                }

                var nextGeneration = new List<int[]>(PopulationSize);

                // 엘리트는 2-opt로 다듬어서 그대로 다음 세대로 넘긴다.
                for (int i = 0; i < eliteCount; i++)
                {
                    nextGeneration.Add(TwoOptImprove(ranked[i].Order, travelTimeSeconds));
                }

                while (nextGeneration.Count < PopulationSize)
                {
                    int[] parentA = TournamentSelect(ranked, random);
                    int[] parentB = TournamentSelect(ranked, random);

                    int[] child = OrderCrossover(parentA, parentB, random);

                    if (random.NextDouble() < MutationRate)
                    {
                        SwapMutate(child, random);
                    }

                    nextGeneration.Add(child);
                }

                population = nextGeneration;
            }

            bestOrder = TwoOptImprove(bestOrder, travelTimeSeconds);
            bestCost = RouteCost(bestOrder, travelTimeSeconds);

            return new TspSolution(bestOrder, bestCost);
        }

        private static double RouteCost(int[] order, double[,] travelTimeSeconds)
        {
            double cost = 0;
            for (int i = 0; i < order.Length - 1; i++)
            {
                cost += travelTimeSeconds[order[i], order[i + 1]];
            }
            return cost;
        }

        private static int[] RandomPermutation(int n, Random random)
        {
            var order = Enumerable.Range(0, n).ToArray();
            for (int i = n - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        private static int[] TournamentSelect(List<(int[] Order, double Cost)> ranked, Random random, int tournamentSize = 4)
        {
            (int[] Order, double Cost) best = ranked[random.Next(ranked.Count)];

            for (int i = 1; i < tournamentSize; i++)
            {
                var candidate = ranked[random.Next(ranked.Count)];
                if (candidate.Cost < best.Cost)
                {
                    best = candidate;
                }
            }

            return best.Order;
        }

        private static int[] OrderCrossover(int[] parentA, int[] parentB, Random random)
        {
            int n = parentA.Length;
            var child = new int[n];
            Array.Fill(child, -1);

            int start = random.Next(n);
            int end = random.Next(n);
            if (start > end)
            {
                (start, end) = (end, start);
            }

            var taken = new HashSet<int>();
            for (int i = start; i <= end; i++)
            {
                child[i] = parentA[i];
                taken.Add(parentA[i]);
            }

            int insertPos = 0;
            foreach (int gene in parentB)
            {
                if (taken.Contains(gene))
                {
                    continue;
                }

                while (insertPos >= start && insertPos <= end)
                {
                    insertPos++;
                }

                child[insertPos] = gene;
                insertPos++;
            }

            return child;
        }

        private static void SwapMutate(int[] order, Random random)
        {
            int i = random.Next(order.Length);
            int j = random.Next(order.Length);
            (order[i], order[j]) = (order[j], order[i]);
        }

        // 열린 경로용 2-opt: 구간 [i+1, j]를 뒤집어 교차를 없앤다 (마지막 지점 이후로 되돌아가는 변은 없음).
        //
        // 실제 도로 이동시간은 방향에 따라 다를 수 있어(비대칭 행렬) 구간을 뒤집으면
        // 그 구간 안의 모든 변의 진행 방향도 함께 바뀐다. 그래서 "바뀌는 경계 두 변만" 비교하는
        // 대칭 TSP용 2-opt 공식은 여기서는 쓸 수 없다 (실제로 써봤더니 비용이 줄었다고 잘못 판단해
        // 같은 구간을 계속 뒤집는 무한루프에 빠졌다). 대신 후보 경로 전체 비용을 다시 계산해 비교한다.
        private static int[] TwoOptImprove(int[] order, double[,] travelTimeSeconds)
        {
            var result = (int[])order.Clone();
            int n = result.Length;
            double currentCost = RouteCost(result, travelTimeSeconds);
            bool improved = true;

            while (improved)
            {
                improved = false;

                for (int i = 0; i < n - 2; i++)
                {
                    for (int j = i + 2; j < n; j++)
                    {
                        Array.Reverse(result, i + 1, j - i);
                        double candidateCost = RouteCost(result, travelTimeSeconds);

                        if (candidateCost < currentCost - 1e-9)
                        {
                            currentCost = candidateCost;
                            improved = true;
                        }
                        else
                        {
                            Array.Reverse(result, i + 1, j - i);
                        }
                    }
                }
            }

            return result;
        }
    }
}
