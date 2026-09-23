namespace TravelApp.WebAPI.Services.RouteOptimization
{
    /// <summary>
    /// 순수 TSP(HeldKarp/OrTools/GeneticAlgorithm)가 구한 방문 순서에, "이 지점은 특정 시각 이전에는
    /// 방문할 수 없다"는 앵커 제약(호텔 체크인 등)을 사후 반영한다.
    ///
    /// 세 solver 모두 시간 개념 없이 순수 이동시간만 최소화하므로, 앵커 시각을 지키기 위해 solver
    /// 내부를 각각 고치는 대신 이 후처리 단계에서 위반된 앵커만 뒤로 밀어 넣는 방식을 쓴다.
    /// 최적성은 보장하지 않지만(다른 지점들의 상호 순서는 건드리지 않음), 최소한의 변경으로
    /// "체크인 이후 시간대에 방문"이라는 제약을 만족시키면서 이미 계산된 좋은 순서를 최대한 보존한다.
    /// </summary>
    public static class AnchorConstraintApplier
    {
        public readonly record struct AnchorResult(int[] Order, double TotalTravelTimeSeconds, bool Adjusted);

        public static AnchorResult Apply(
            IReadOnlyList<int> order,
            double[,] travelTimeSeconds,
            DateTime departureTime,
            IReadOnlyDictionary<int, DateTime> anchorTimesByIndex)
        {
            var result = new List<int>(order);

            if (anchorTimesByIndex.Count == 0)
            {
                return new AnchorResult(result.ToArray(), TotalCost(result, travelTimeSeconds), false);
            }

            bool adjusted = false;
            int position = 0;

            // 앵커 하나당 재배치는 최대 한 번만 시도한다. 그렇지 않으면, 맨 뒤로 옮겨도 여전히
            // 요구 시각을 못 맞추는 앵커(다른 지점을 전부 방문해도 시간이 모자란 경우)가 같은
            // 자리로 계속 제거/재삽입되며 무한 루프에 빠진다.
            var relocated = new HashSet<int>();

            while (position < result.Count)
            {
                int node = result[position];

                if (!relocated.Contains(node) &&
                    anchorTimesByIndex.TryGetValue(node, out DateTime requiredTime) &&
                    ArrivalTimeAt(result, position, departureTime, travelTimeSeconds) < requiredTime)
                {
                    adjusted = true;
                    relocated.Add(node);
                    result.RemoveAt(position);

                    // 제거된 자리(position)부터, 이 지점을 끼워 넣었을 때 요구 시각을 만족하는
                    // 첫 위치를 찾는다. 끝까지 못 찾으면 맨 뒤에 넣는다(도착해서 잠깐 기다리게 된다).
                    int insertAt = position;
                    while (insertAt < result.Count &&
                           ArrivalTimeIfInsertedAt(result, insertAt, node, departureTime, travelTimeSeconds) < requiredTime)
                    {
                        insertAt++;
                    }

                    result.Insert(insertAt, node);

                    // 삽입으로 인해 이 자리에 새로 온 지점도 다시 검사해야 한다(연쇄적으로
                    // 다른 앵커의 도착 시각이 당겨져 위반이 생길 수 있으므로 position은 그대로 둔다).
                    continue;
                }

                position++;
            }

            return new AnchorResult(result.ToArray(), TotalCost(result, travelTimeSeconds), adjusted);
        }

        // order[0..position]까지 순서대로 방문했을 때 order[position]에 도착하는 시각.
        private static DateTime ArrivalTimeAt(
            List<int> order, int position, DateTime departureTime, double[,] travelTimeSeconds)
        {
            DateTime clock = departureTime;
            for (int i = 1; i <= position; i++)
            {
                clock = clock.AddSeconds(travelTimeSeconds[order[i - 1], order[i]]);
            }
            return clock;
        }

        // node가 빠진 orderWithoutNode에 insertAt 위치로 끼워 넣었을 때 node에 도착하는 시각.
        private static DateTime ArrivalTimeIfInsertedAt(
            List<int> orderWithoutNode, int insertAt, int node, DateTime departureTime, double[,] travelTimeSeconds)
        {
            if (insertAt == 0)
            {
                return departureTime;
            }

            DateTime arrivalAtPrevious = ArrivalTimeAt(orderWithoutNode, insertAt - 1, departureTime, travelTimeSeconds);
            return arrivalAtPrevious.AddSeconds(travelTimeSeconds[orderWithoutNode[insertAt - 1], node]);
        }

        private static double TotalCost(List<int> order, double[,] travelTimeSeconds)
        {
            double total = 0;
            for (int i = 1; i < order.Count; i++)
            {
                total += travelTimeSeconds[order[i - 1], order[i]];
            }
            return total;
        }
    }
}
