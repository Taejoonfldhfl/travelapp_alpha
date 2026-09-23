using TravelApp.WebAPI.Services.RouteOptimization;

namespace TravelApp.WebAPI.Tests
{
    public class AnchorConstraintApplierTests
    {
        private static readonly DateTime DepartureTime = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Apply_withNoAnchors_returnsOrderUnchanged()
        {
            double[,] matrix =
            {
                { 0, 100, 200 },
                { 100, 0, 150 },
                { 200, 150, 0 }
            };
            int[] order = { 0, 1, 2 };

            var result = AnchorConstraintApplier.Apply(
                order, matrix, DepartureTime, new Dictionary<int, DateTime>());

            Assert.Equal(order, result.Order);
            Assert.False(result.Adjusted);
            Assert.Equal(250, result.TotalTravelTimeSeconds);
        }

        [Fact]
        public void Apply_whenAnchorAlreadySatisfied_leavesOrderUnchanged()
        {
            // 0 -> 1 -> 2(앵커), 앵커 요구 시각이 자연스러운 도착 시각보다 이르면 손댈 필요가 없다.
            double[,] matrix =
            {
                { 0, 3600, 7200 },
                { 3600, 0, 3600 },
                { 7200, 3600, 0 }
            };
            int[] order = { 0, 1, 2 };
            var anchors = new Dictionary<int, DateTime> { [2] = DepartureTime.AddHours(1) };

            var result = AnchorConstraintApplier.Apply(order, matrix, DepartureTime, anchors);

            Assert.Equal(order, result.Order);
            Assert.False(result.Adjusted);
        }

        [Fact]
        public void Apply_whenAnchorVisitedTooEarly_movesItLaterInTheOrder()
        {
            // 자연스러운 순서는 앵커(2)를 맨 처음 방문하지만, 체크인은 5시간 뒤에나 가능하다.
            // 다른 지점(0, 1)을 먼저 방문한 뒤 체크인하도록 순서가 조정돼야 한다.
            double[,] matrix =
            {
                //   anchor  0      1
                { 0, 600, 1200 },   // anchor(index 0) -> 0, 1
                { 600, 0, 900 },    // 0 -> anchor, 1
                { 1200, 900, 0 }    // 1 -> anchor, 0
            };
            int anchorIndex = 0;
            int[] naturalOrder = { anchorIndex, 1, 2 };
            var anchors = new Dictionary<int, DateTime> { [anchorIndex] = DepartureTime.AddHours(5) };

            var result = AnchorConstraintApplier.Apply(naturalOrder, matrix, DepartureTime, anchors);

            Assert.True(result.Adjusted);
            Assert.Equal(anchorIndex, result.Order[^1]);
            Assert.DoesNotContain(anchorIndex, result.Order.Take(result.Order.Length - 1));
        }

        [Fact]
        public void Apply_whenEvenLastPositionArrivesTooEarly_stillPlacesAnchorLast()
        {
            // 나머지 지점을 전부 방문해도 체크인 시각에 한참 못 미친다 -> 맨 뒤에 넣고 기다리는 수밖에 없다.
            double[,] matrix =
            {
                { 0, 60, 60 },
                { 60, 0, 60 },
                { 60, 60, 0 }
            };
            int anchorIndex = 0;
            int[] naturalOrder = { anchorIndex, 1, 2 };
            var anchors = new Dictionary<int, DateTime> { [anchorIndex] = DepartureTime.AddHours(10) };

            var result = AnchorConstraintApplier.Apply(naturalOrder, matrix, DepartureTime, anchors);

            Assert.True(result.Adjusted);
            Assert.Equal(anchorIndex, result.Order[^1]);
        }

        [Fact]
        public void Apply_recomputesTotalTravelTime_afterReordering()
        {
            double[,] matrix =
            {
                { 0, 600, 1200 },
                { 600, 0, 900 },
                { 1200, 900, 0 }
            };
            int anchorIndex = 0;
            int[] naturalOrder = { anchorIndex, 1, 2 };
            var anchors = new Dictionary<int, DateTime> { [anchorIndex] = DepartureTime.AddHours(5) };

            var result = AnchorConstraintApplier.Apply(naturalOrder, matrix, DepartureTime, anchors);

            double expectedTotal = 0;
            for (int i = 1; i < result.Order.Length; i++)
            {
                expectedTotal += matrix[result.Order[i - 1], result.Order[i]];
            }
            Assert.Equal(expectedTotal, result.TotalTravelTimeSeconds);
        }
    }
}
