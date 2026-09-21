using Microsoft.EntityFrameworkCore;
using SharedData.DTOs;
using SharedData.Models;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services.RouteOptimization.TspSolvers;

namespace TravelApp.WebAPI.Services.RouteOptimization
{
    // 1단계 스코프: 우선순위/필수방문 여부는 무시하고, 하루 안에 등록된 장소를 전부 방문하는
    // 최소 이동시간 순서를 구한다 (고전적 TSP, 스킵/선택 로직 없음).
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
                    SkippedSchedulesWithoutCoordinates = skipped
                };
            }

            var points = withCoordinates
                .Select(s => new GeoPoint(s.Id, s.Latitude!.Value, s.Longitude!.Value))
                .ToList();

            // 정체 상황이 시간대에 따라 다르므로, 그날 일정 중 가장 빠른 시작 시각을 출발 기준으로 쓴다.
            DateTime departureTime = withCoordinates.Min(s => s.StartTime);

            double[,] travelTimeSeconds = await _travelTimeProvider.GetTravelTimeMatrixAsync(
                points, departureTime, cancellationToken);

            (TspSolution solution, string solverUsed) = Solve(travelTimeSeconds);

            var orderedSchedules = solution.Order.Select(idx => withCoordinates[idx]).ToList();

            var stops = new List<RouteOptimizationStopDto>();
            for (int i = 0; i < orderedSchedules.Count; i++)
            {
                double legSeconds = i == 0 ? 0 : travelTimeSeconds[solution.Order[i - 1], solution.Order[i]];
                stops.Add(ToStop(orderedSchedules[i], i, legSeconds));
            }

            if (apply)
            {
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
                TotalTravelTimeSeconds = solution.TotalTravelTimeSeconds,
                SolverUsed = solverUsed,
                Applied = apply,
                SkippedSchedulesWithoutCoordinates = skipped
            };
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
                TravelTimeFromPreviousSeconds = legSeconds
            };
        }
    }
}
