using Microsoft.EntityFrameworkCore;
using SharedData.DTOs;
using SharedData.Models;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services.RouteOptimization.TspSolvers;

namespace TravelApp.WebAPI.Services.RouteOptimization
{
    // 1단계: 하루 안에 등록된 장소를 전부 방문하는 최소 이동시간 순서를 구한다(고전적 TSP).
    // 2단계: 그 결과가 하루 시간 예산을 넘을 것으로 예상되면 PriorityBudgetPlanner가 우선순위 낮은
    // 일정부터 제외해가며 예산 안에 들어오는 조합을 찾는다. TSP solver/AnchorConstraintApplier
    // 자체는 그대로 두고, 그 위에 "어떤 부분집합을 넣을지"를 결정하는 레이어만 추가한 구조다.
    public class RouteOptimizationService
    {
        // N<=10 -> Held-Karp로 정확한 최적해. N>10 -> OR-Tools를 짧은 시간예산으로 우선 시도.
        private const int HeldKarpMaxSize = 10;
        private static readonly TimeSpan OrToolsTimeBudget = TimeSpan.FromMilliseconds(250);

        private readonly ApplicationDbContext _context;
        private readonly ITravelTimeProvider _travelTimeProvider;

        public RouteOptimizationService(ApplicationDbContext context, ITravelTimeProvider travelTimeProvider)
        {
            _context = context;
            _travelTimeProvider = travelTimeProvider;
        }

        public async Task<RouteOptimizationResultDto?> OptimizeAsync(
            int tripId,
            DateTime date,
            bool apply,
            TimeSpan? dayStartTime = null,
            TimeSpan? dayEndTime = null,
            CancellationToken cancellationToken = default)
        {
            var allSchedulesForDay = await _context.Schedules
                .Where(s => s.TripId == tripId && s.StartTime.Date == date.Date)
                .OrderBy(s => s.StartTime)
                .ThenBy(s => s.Order)
                .ToListAsync(cancellationToken);

            if (allSchedulesForDay.Count == 0)
            {
                return null;
            }

            var withCoordinates = allSchedulesForDay
                .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
                .ToList();

            var skipped = allSchedulesForDay
                .Where(s => !s.Latitude.HasValue || !s.Longitude.HasValue)
                .Select(s => s.Title)
                .ToList();

            if (withCoordinates.Count <= 1)
            {
                return new RouteOptimizationResultDto
                {
                    Date = date.Date,
                    Stops = withCoordinates
                        .Select((s, i) => ToStop(s, i, 0))
                        .ToList(),
                    TotalTravelTimeSeconds = 0,
                    SolverUsed = "None",
                    Applied = false,
                    SkippedSchedulesWithoutCoordinates = skipped,
                    AnchorAdjusted = false
                };
            }

            var points = withCoordinates
                .Select(s => new GeoPoint(s.Id, s.Latitude!.Value, s.Longitude!.Value))
                .ToList();

            // 정체 상황이 시간대에 따라 다르므로, 그날 일정 중 가장 빠른 시작 시각을 출발 기준으로 쓴다.
            DateTime departureTime = withCoordinates.Min(s => s.StartTime);

            double[,] travelTimeSeconds = await _travelTimeProvider.GetTravelTimeMatrixAsync(
                points, departureTime, cancellationToken);

            // 하루에 배정 가능한 시간(예산). 사용자가 활동 시작~종료 시간 범위를 지정했으면 그것을
            // 쓰고, 지정하지 않았으면 그날 이미 등록된 일정들의 시작~종료 범위(가장 이른 시작 ~
            // 가장 늦은 종료)를 기본 예산으로 삼는다. 스킵 로직이 예산을 스스로 줄여가며 "항상
            // 통과"하는 상황을 막기 위해, 예산은 스킵을 시작하기 전 원래 전체 일정 기준으로 한 번만
            // 계산하고 이후 고정한다.
            TimeSpan budget = ComputeDayBudget(withCoordinates, date, dayStartTime, dayEndTime);

            var budgetInfos = withCoordinates
                .Select((s, idx) => new PriorityBudgetPlanner.ScheduleBudgetInfo(
                    Index: idx,
                    ScheduleId: s.Id,
                    Title: s.Title,
                    Priority: s.Priority,
                    IsEssential: s.IsEssential,
                    IsHotelCheckIn: s.IsHotelCheckIn,
                    Duration: s.EndTime > s.StartTime ? s.EndTime - s.StartTime : TimeSpan.Zero))
                .ToList();

            var planResult = PriorityBudgetPlanner.Plan(
                budgetInfos,
                budget,
                includedIndices => SolveSubset(includedIndices, withCoordinates, travelTimeSeconds, departureTime)
                    .TotalTravelTimeSeconds);

            var finalSolve = SolveSubset(planResult.IncludedIndices, withCoordinates, travelTimeSeconds, departureTime);

            var orderedSchedules = finalSolve.OrderInFullIndices.Select(idx => withCoordinates[idx]).ToList();

            var stops = new List<RouteOptimizationStopDto>();
            for (int i = 0; i < orderedSchedules.Count; i++)
            {
                double legSeconds = i == 0
                    ? 0
                    : travelTimeSeconds[finalSolve.OrderInFullIndices[i - 1], finalSolve.OrderInFullIndices[i]];
                stops.Add(ToStop(orderedSchedules[i], i, legSeconds));
            }

            if (apply)
            {
                // 우선순위/예산 로직으로 제외된 일정은 이번 결과에 포함되지 않았을 뿐 삭제된 게
                // 아니므로, 그 일정의 기존 Order는 건드리지 않는다(다음 날짜 재계산이나 사용자의
                // 수동 조정에서 계속 의미를 갖도록).
                for (int i = 0; i < orderedSchedules.Count; i++)
                {
                    orderedSchedules[i].Order = i;
                }
                await _context.SaveChangesAsync(cancellationToken);
            }

            return new RouteOptimizationResultDto
            {
                Date = date.Date,
                Stops = stops,
                TotalTravelTimeSeconds = finalSolve.TotalTravelTimeSeconds,
                SolverUsed = finalSolve.SolverUsed,
                Applied = apply,
                SkippedSchedulesWithoutCoordinates = skipped,
                AnchorAdjusted = finalSolve.AnchorAdjusted,
                SkippedSchedulesByPriority = planResult.Skipped
                    .Select(s => new SkippedScheduleDto
                    {
                        ScheduleId = s.ScheduleId,
                        Title = s.Title,
                        Priority = s.Priority,
                        Reason = s.Reason
                    })
                    .ToList(),
                ExceedsTimeBudget = planResult.ExceedsTimeBudget
            };
        }

        private static TimeSpan ComputeDayBudget(
            List<Schedule> withCoordinates, DateTime date, TimeSpan? dayStartTime, TimeSpan? dayEndTime)
        {
            DateTime budgetStart;
            DateTime budgetEnd;

            if (dayStartTime.HasValue && dayEndTime.HasValue && dayEndTime.Value > dayStartTime.Value)
            {
                budgetStart = date.Date + dayStartTime.Value;
                budgetEnd = date.Date + dayEndTime.Value;
            }
            else
            {
                budgetStart = withCoordinates.Min(s => s.StartTime);
                budgetEnd = withCoordinates.Max(s => s.EndTime);
            }

            return budgetEnd > budgetStart ? budgetEnd - budgetStart : TimeSpan.Zero;
        }

        // 일정 부분집합(fullIndices, withCoordinates 기준 원래 인덱스들)만으로 경로를 구한다.
        // 기존 Solve()와 AnchorConstraintApplier를 그대로 재사용하고, 부분집합 전용 로컬 인덱스로
        // 앵커를 다시 매핑한 뒤 결과를 다시 원래 인덱스로 되돌린다. PriorityBudgetPlanner가 스킵
        // 후보를 하나씩 빼가며 여러 번 호출하고, 마지막으로 확정된 집합에 대해서도 한 번 더 호출된다.
        private (int[] OrderInFullIndices, double TotalTravelTimeSeconds, string SolverUsed, bool AnchorAdjusted) SolveSubset(
            IReadOnlyList<int> fullIndices,
            List<Schedule> withCoordinates,
            double[,] travelTimeSeconds,
            DateTime departureTime)
        {
            if (fullIndices.Count == 0)
            {
                return (Array.Empty<int>(), 0, "None", false);
            }

            if (fullIndices.Count == 1)
            {
                return (new[] { fullIndices[0] }, 0, "None", false);
            }

            double[,] subMatrix = BuildSubMatrix(travelTimeSeconds, fullIndices);
            (TspSolution solution, string solverUsed) = Solve(subMatrix);

            var anchorTimesByIndex = fullIndices
                .Select((fullIdx, subIdx) => (FullIdx: fullIdx, SubIdx: subIdx))
                .Where(x => withCoordinates[x.FullIdx].IsHotelCheckIn)
                .ToDictionary(x => x.SubIdx, x => withCoordinates[x.FullIdx].StartTime);

            int[] finalSubOrder = solution.Order;
            double totalTravelTimeSeconds = solution.TotalTravelTimeSeconds;
            bool anchorAdjusted = false;

            if (anchorTimesByIndex.Count > 0)
            {
                var anchorResult = AnchorConstraintApplier.Apply(
                    solution.Order, subMatrix, departureTime, anchorTimesByIndex);
                finalSubOrder = anchorResult.Order;
                totalTravelTimeSeconds = anchorResult.TotalTravelTimeSeconds;
                anchorAdjusted = anchorResult.Adjusted;
            }

            int[] orderInFullIndices = finalSubOrder.Select(subIdx => fullIndices[subIdx]).ToArray();
            return (orderInFullIndices, totalTravelTimeSeconds, solverUsed, anchorAdjusted);
        }

        private static double[,] BuildSubMatrix(double[,] full, IReadOnlyList<int> indices)
        {
            int n = indices.Count;
            var sub = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    sub[i, j] = full[indices[i], indices[j]];
                }
            }
            return sub;
        }

        private static (TspSolution Solution, string SolverUsed) Solve(double[,] travelTimeSeconds)
        {
            int n = travelTimeSeconds.GetLength(0);

            if (n <= HeldKarpMaxSize)
            {
                return (HeldKarpSolver.Solve(travelTimeSeconds), "HeldKarp");
            }

            var orToolsSolution = OrToolsSolver.TrySolve(travelTimeSeconds, OrToolsTimeBudget);
            if (orToolsSolution != null)
            {
                return (orToolsSolution, "OrTools");
            }

            return (GeneticAlgorithmSolver.Solve(travelTimeSeconds), "GeneticAlgorithm");
        }

        private static RouteOptimizationStopDto ToStop(Schedule schedule, int visitOrder, double legSeconds)
        {
            return new RouteOptimizationStopDto
            {
                ScheduleId = schedule.Id,
                Title = schedule.Title,
                PlaceName = schedule.PlaceName,
                Latitude = schedule.Latitude!.Value,
                Longitude = schedule.Longitude!.Value,
                VisitOrder = visitOrder,
                TravelTimeFromPreviousSeconds = legSeconds,
                IsHotelCheckIn = schedule.IsHotelCheckIn
            };
        }
    }
}
