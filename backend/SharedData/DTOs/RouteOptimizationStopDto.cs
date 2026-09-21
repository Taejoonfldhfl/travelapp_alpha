using System;

namespace SharedData.DTOs
{
    // 경로 최적화 결과에서 방문 순서 하나를 나타낸다.
    public class RouteOptimizationStopDto
    {
        public int ScheduleId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string PlaceName { get; set; } = string.Empty;

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        // 0부터 시작하는 방문 순서.
        public int VisitOrder { get; set; }

        // 이전 방문지에서 여기까지 걸리는 이동시간(초). 첫 방문지는 0.
        public double TravelTimeFromPreviousSeconds { get; set; }
    }
}
