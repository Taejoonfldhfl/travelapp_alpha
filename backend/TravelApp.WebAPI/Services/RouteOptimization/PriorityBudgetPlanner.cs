namespace TravelApp.WebAPI.Services.RouteOptimization
{
    /// <summary>
    /// AnchorConstraintApplier와 마찬가지로 기존 TSP solver(HeldKarp/OrTools/GeneticAlgorithm)는
    /// 전혀 건드리지 않고 그 위에 얹는 별개 레이어다: 하루에 배정 가능한 시간(예산)을 초과할 것으로
    /// 예상되면, 우선순위가 낮은 일정부터 하나씩 제외해가며 다시 계산해서 예산 안에 들어오는
    /// 조합을 찾는다. solver 자체를 모르는 순수 함수라 실제 경로 계산 없이도 단위테스트할 수 있다.
    ///
    /// 설계 판단 1 (우선순위 규칙): Priority는 숫자가 클수록 더 중요하다(먼저 살아남는다) —
    /// 즉 가장 작은 Priority를 가진, IsEssential=false인 일정부터 제외 후보가 된다. 동점이면
    /// Duration(활동 시간)이 긴 일정을 먼저 제외한다 — 한 번의 제외로 더 많은 시간을 확보해
    /// 재계산 횟수를 줄이기 위함이다. 그래도 동점이면 ScheduleId 오름차순으로 결정적으로 정렬한다.
    ///
    /// 설계 판단 2 (앵커 vs 우선순위): 호텔 체크인 등 앵커 제약이 걸린 일정(IsHotelCheckIn=true)은
    /// Priority/IsEssential 값과 무관하게 항상 "제외 불가"로 취급한다. 체크인은 실제로 지켜야 하는
    /// 약속이라, 우선순위가 낮게 설정돼 있거나 실수로 IsEssential=false로 저장돼 있어도 스킵 후보에서
    /// 제외되어야 AnchorConstraintApplier가 기대하는 "앵커는 항상 방문한다"는 전제와 모순되지 않는다.
    /// </summary>
    public static class PriorityBudgetPlanner
    {
        public readonly record struct ScheduleBudgetInfo(
            int Index,
            int ScheduleId,
            string Title,
            int Priority,
            bool IsEssential,
            bool IsHotelCheckIn,
            TimeSpan Duration);

        public readonly record struct SkippedScheduleInfo(int ScheduleId, string Title, int Priority, string Reason);

        public readonly record struct PlanResult(
            List<int> IncludedIndices,
            List<SkippedScheduleInfo> Skipped,
            bool ExceedsTimeBudget);

        public const string SkipReason = "예상 소요 시간이 하루 활동 시간 예산을 초과해 우선순위가 낮은 일정부터 제외됨";

        /// <param name="schedules">그날 좌표가 있는 일정 전부(경로 최적화 대상).</param>
        /// <param name="budget">하루에 배정 가능한 총 시간(이동시간 + 활동시간의 상한).</param>
        /// <param name="solveTravelTimeSeconds">
        /// 남은 일정들의 인덱스 부분집합(원래 순서를 유지한 리스트)을 주면, 그 부분집합만으로 구한
        /// 최적 경로의 총 이동시간(초)을 돌려주는 함수. 실제 서비스에서는 기존 Solve()와
        /// AnchorConstraintApplier를 그대로 재사용해 구현한다 — 이 클래스는 solver를 전혀 모른다.
        /// </param>
        public static PlanResult Plan(
            IReadOnlyList<ScheduleBudgetInfo> schedules,
            TimeSpan budget,
            Func<IReadOnlyList<int>, double> solveTravelTimeSeconds)
        {
            var included = schedules.Select(s => s.Index).ToList();

            var skipCandidates = schedules
                .Where(s => !s.IsEssential && !s.IsHotelCheckIn)
                .OrderBy(s => s.Priority)
                .ThenByDescending(s => s.Duration)
                .ThenBy(s => s.ScheduleId)
                .ToList();

            var byIndex = schedules.ToDictionary(s => s.Index);
            var skipped = new List<SkippedScheduleInfo>();
            double budgetSeconds = Math.Max(0, budget.TotalSeconds);
            int nextCandidatePosition = 0;

            while (true)
            {
                double requiredSeconds = RequiredSeconds(included, byIndex, solveTravelTimeSeconds);

                if (requiredSeconds <= budgetSeconds)
                {
                    return new PlanResult(included, skipped, ExceedsTimeBudget: false);
                }

                while (nextCandidatePosition < skipCandidates.Count &&
                       !included.Contains(skipCandidates[nextCandidatePosition].Index))
                {
                    nextCandidatePosition++;
                }

                if (nextCandidatePosition >= skipCandidates.Count)
                {
                    // 더 뺄 수 있는(비필수·비앵커) 일정이 없다 -> 남은 건 전부 Must/앵커.
                    // 아무것도 추가로 제외하지 않고 전부 포함한 채 "시간 초과"만 알린다.
                    return new PlanResult(included, skipped, ExceedsTimeBudget: true);
                }

                var candidate = skipCandidates[nextCandidatePosition];
                included.Remove(candidate.Index);
                skipped.Add(new SkippedScheduleInfo(candidate.ScheduleId, candidate.Title, candidate.Priority, SkipReason));
            }
        }

        private static double RequiredSeconds(
            List<int> included,
            Dictionary<int, ScheduleBudgetInfo> byIndex,
            Func<IReadOnlyList<int>, double> solveTravelTimeSeconds)
        {
            if (included.Count == 0)
            {
                return 0;
            }

            double travelSeconds = included.Count == 1 ? 0 : solveTravelTimeSeconds(included);
            double activitySeconds = included.Sum(idx => Math.Max(0, byIndex[idx].Duration.TotalSeconds));
            return travelSeconds + activitySeconds;
        }
    }
}
