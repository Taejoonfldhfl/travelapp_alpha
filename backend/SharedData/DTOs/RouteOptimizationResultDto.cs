using System;
using System.Collections.Generic;

namespace SharedData.DTOs
{
    public class RouteOptimizationResultDto
    {
        public DateTime Date { get; set; }

        public List<RouteOptimizationStopDto> Stops { get; set; } = new();

        public double TotalTravelTimeSeconds { get; set; }

        // 어떤 알고리즘이 최종 순서를 결정했는지 (HeldKarp / OrTools / GeneticAlgorithm).
        public string SolverUsed { get; set; } = string.Empty;

        // apply=true로 호출해 일정의 Order 필드에 이 결과를 실제로 반영했는지 여부.
        public bool Applied { get; set; }

        // 위도/경도가 없어 이번 최적화에서 제외된 일정들 (제목만 안내).
        public List<string> SkippedSchedulesWithoutCoordinates { get; set; } = new();
    }
}
