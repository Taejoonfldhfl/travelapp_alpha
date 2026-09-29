using TravelApp.WebAPI.Services.RouteOptimization;
using static TravelApp.WebAPI.Services.RouteOptimization.PriorityBudgetPlanner;

namespace TravelApp.WebAPI.Tests
{
    public class PriorityBudgetPlannerTests
    {
        // 모든 케이스에서 solveTravelTimeSeconds는 "포함된 지점 수 * 10분"으로 단순화한 가짜
        // solver다(실제 TSP를 몰라도 스킵 로직 자체를 검증할 수 있게). 지점이 늘수록 이동시간도
        // 늘어난다는 점만 사실이면 충분하다.
        private static double FakeTravelSeconds(IReadOnlyList<int> included) => included.Count * 600;

        private static ScheduleBudgetInfo Info(
            int index, int priority = 0, bool isEssential = false, bool isHotelCheckIn = false, int durationMinutes = 60) =>
            new(index, ScheduleId: index + 1, Title: $"일정{index}", Priority: priority,
                IsEssential: isEssential, IsHotelCheckIn: isHotelCheckIn,
                Duration: TimeSpan.FromMinutes(durationMinutes));

        [Fact]
        public void Plan_whenWithinBudget_skipsNothing()
        {
            var schedules = new List<ScheduleBudgetInfo>
            {
                Info(0, priority: 1, durationMinutes: 60),
                Info(1, priority: 2, durationMinutes: 60),
            };

            // 이동시간(2*600=1200초=20분) + 활동시간(120분) = 140분. 예산을 넉넉하게 준다.
            var result = Plan(schedules, TimeSpan.FromMinutes(200), FakeTravelSeconds);

            Assert.Equal(new[] { 0, 1 }, result.IncludedIndices);
            Assert.Empty(result.Skipped);
            Assert.False(result.ExceedsTimeBudget);
        }

        [Fact]
        public void Plan_whenOverBudget_skipsLowestPriorityFirst()
        {
            var schedules = new List<ScheduleBudgetInfo>
            {
                Info(0, priority: 5, durationMinutes: 60), // 가장 중요
                Info(1, priority: 1, durationMinutes: 60), // 가장 덜 중요 -> 먼저 스킵되어야 함
                Info(2, priority: 3, durationMinutes: 60),
            };

            // 3개 모두 포함 시 이동시간 30분 + 활동시간 180분 = 210분. 예산은 150분뿐이라 하나는 빠져야 함.
            // 2개만 남으면 이동시간 20분 + 활동시간 120분 = 140분으로 예산(150분) 안에 들어온다.
            var result = Plan(schedules, TimeSpan.FromMinutes(150), FakeTravelSeconds);

            Assert.False(result.ExceedsTimeBudget);
            Assert.Single(result.Skipped);
            Assert.Equal(1, result.Skipped[0].ScheduleId - 1); // index 1 (priority 1) skipped
            Assert.DoesNotContain(1, result.IncludedIndices);
            Assert.Contains(0, result.IncludedIndices);
            Assert.Contains(2, result.IncludedIndices);
        }

        [Fact]
        public void Plan_neverSkipsEssentialSchedules_evenWithLowestPriority()
        {
            var schedules = new List<ScheduleBudgetInfo>
            {
                Info(0, priority: 0, isEssential: true, durationMinutes: 60), // Priority 최하지만 Must
                Info(1, priority: 10, durationMinutes: 60),
            };

            // 예산을 0분으로 줘서 뭐든 하나는 빠져야 하는 극단적인 상황을 만든다.
            var result = Plan(schedules, TimeSpan.Zero, FakeTravelSeconds);

            Assert.Contains(0, result.IncludedIndices); // Must는 끝까지 남는다
            Assert.Single(result.Skipped);
            Assert.Equal(2, result.Skipped[0].ScheduleId); // index 1 (Must 아님) 이 대신 빠짐
        }

        [Fact]
        public void Plan_neverSkipsHotelCheckInAnchors_regardlessOfPriorityOrEssentialFlag()
        {
            // 앵커(호텔 체크인)는 Priority가 가장 낮고 IsEssential=false로 "실수로" 저장돼 있어도
            // 스킵 후보에서 제외되어야 한다(설계 판단 2).
            var schedules = new List<ScheduleBudgetInfo>
            {
                Info(0, priority: 0, isEssential: false, isHotelCheckIn: true, durationMinutes: 60),
                Info(1, priority: 5, durationMinutes: 60),
            };

            var result = Plan(schedules, TimeSpan.Zero, FakeTravelSeconds);

            Assert.Contains(0, result.IncludedIndices); // 앵커는 끝까지 남는다
            Assert.Single(result.Skipped);
            Assert.Equal(2, result.Skipped[0].ScheduleId); // 앵커가 아닌 index 1이 대신 빠짐
        }

        [Fact]
        public void Plan_whenAllRemainingAreEssential_stopsSkippingAndReturnsExceedsTimeBudget()
        {
            var schedules = new List<ScheduleBudgetInfo>
            {
                Info(0, isEssential: true, durationMinutes: 120),
                Info(1, isEssential: true, durationMinutes: 120),
                Info(2, isHotelCheckIn: true, durationMinutes: 60),
            };

            // 셋 다 Must/앵커라 물리적으로 시간이 부족해도(예산 1분) 아무것도 못 뺀다.
            var result = Plan(schedules, TimeSpan.FromMinutes(1), FakeTravelSeconds);

            Assert.True(result.ExceedsTimeBudget);
            Assert.Empty(result.Skipped);
            Assert.Equal(new[] { 0, 1, 2 }, result.IncludedIndices.OrderBy(x => x));
        }

        [Fact]
        public void Plan_whenSkippingAllOptionalStillExceedsBudget_stopsAtMustOnlySetWithWarning()
        {
            var schedules = new List<ScheduleBudgetInfo>
            {
                Info(0, isEssential: true, durationMinutes: 200), // Must 혼자서도 이미 예산 초과
                Info(1, priority: 1, durationMinutes: 60),
                Info(2, priority: 2, durationMinutes: 60),
            };

            var result = Plan(schedules, TimeSpan.FromMinutes(10), FakeTravelSeconds);

            // 비필수 2개는 전부 스킵되지만, 그래도 Must 하나만으로 예산을 넘기므로 경고가 남는다.
            Assert.True(result.ExceedsTimeBudget);
            Assert.Equal(2, result.Skipped.Count);
            Assert.Equal(new[] { 0 }, result.IncludedIndices);
        }

        [Fact]
        public void Plan_tieBreaksBySameLowestPriority_prefersSkippingLongerDurationFirst()
        {
            var schedules = new List<ScheduleBudgetInfo>
            {
                Info(0, priority: 1, durationMinutes: 30),
                Info(1, priority: 1, durationMinutes: 90), // 같은 우선순위, 더 긴 활동시간 -> 먼저 스킵
            };

            // 하나만 빠지면 충분하도록 예산을 좁게 잡는다.
            var result = Plan(schedules, TimeSpan.FromMinutes(50), FakeTravelSeconds);

            Assert.Single(result.Skipped);
            Assert.Equal(2, result.Skipped[0].ScheduleId); // index 1 (90분짜리)이 먼저 빠짐
            Assert.Contains(0, result.IncludedIndices);
        }
    }
}
