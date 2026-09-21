using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes;

namespace TravelApp.WebAPI.Services.RouteOptimization.TspSolvers
{
    // N>10일 때 우선 시도하는 solver. 정해진 시간예산(기본 250ms) 안에서 해를 찾는다.
    // OR-Tools의 RoutingModel은 기본적으로 닫힌 순환(출발지로 복귀)을 최적화하므로,
    // "가상의 허브 노드"를 하나 추가해 실제 지점들 사이의 왕복 제약을 없애는 표준 트릭으로
    // 열린 경로(시작/끝 자유) TSP를 표현한다. 허브에서 나가는/들어오는 비용은 모두 0으로 둔다.
    public static class OrToolsSolver
    {
        public static TspSolution? TrySolve(double[,] travelTimeSeconds, TimeSpan timeBudget)
        {
            int n = travelTimeSeconds.GetLength(0);

            if (n <= 1)
            {
                return new TspSolution(Enumerable.Range(0, n).ToArray(), 0);
            }

            // 허브 노드(인덱스 0)를 추가한 (n+1) x (n+1) 확장 행렬.
            int extendedSize = n + 1;
            const int hub = 0;

            long[,] costMatrix = new long[extendedSize, extendedSize];
            for (int i = 0; i < extendedSize; i++)
            {
                for (int j = 0; j < extendedSize; j++)
                {
                    if (i == j)
                    {
                        costMatrix[i, j] = 0;
                    }
                    else if (i == hub || j == hub)
                    {
                        // 허브 <-> 실제 지점 이동은 비용이 없다: 실제 지점들 중 어디서 시작/끝나도 동일하게 취급.
                        costMatrix[i, j] = 0;
                    }
                    else
                    {
                        // 실제 지점 인덱스는 확장 행렬에서 1씩 밀려 있다 (0번은 허브).
                        costMatrix[i, j] = (long)Math.Round(travelTimeSeconds[i - 1, j - 1] * 1000);
                    }
                }
            }

            var manager = new RoutingIndexManager(extendedSize, 1, hub);
            var routing = new RoutingModel(manager);

            int transitCallbackIndex = routing.RegisterTransitCallback((long fromIndex, long toIndex) =>
            {
                int fromNode = manager.IndexToNode(fromIndex);
                int toNode = manager.IndexToNode(toIndex);
                return costMatrix[fromNode, toNode];
            });

            routing.SetArcCostEvaluatorOfAllVehicles(transitCallbackIndex);

            var searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
            searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
            searchParameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
            searchParameters.TimeLimit = Duration.FromTimeSpan(timeBudget);

            Assignment? solution = routing.SolveWithParameters(searchParameters);

            if (solution == null)
            {
                return null;
            }

            var order = new List<int>();
            long index = routing.Start(0);

            while (!routing.IsEnd(index))
            {
                int node = manager.IndexToNode(index);
                if (node != hub)
                {
                    order.Add(node - 1);
                }
                index = solution.Value(routing.NextVar(index));
            }

            if (order.Count != n)
            {
                // 방문 전부를 못 채웠으면 상위 로직에서 GA로 폴백하도록 실패로 취급한다.
                return null;
            }

            double totalCost = 0;
            for (int i = 0; i < order.Count - 1; i++)
            {
                totalCost += travelTimeSeconds[order[i], order[i + 1]];
            }

            return new TspSolution(order.ToArray(), totalCost);
        }
    }
}
