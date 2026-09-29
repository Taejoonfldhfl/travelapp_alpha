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

        // 호텔 체크인 등 앵커 제약 때문에 순수 최소이동시간 순서에서 방문 순서가 조정됐는지 여부.
        public bool AnchorAdjusted { get; set; }

        // 우선순위/시간예산 로직 때문에 이번 결과에서 제외된 일정들과 사유. 좌표가 없어서 애초에
        // 대상이 아니었던 SkippedSchedulesWithoutCoordinates와는 별개다.
        public List<SkippedScheduleDto> SkippedSchedulesByPriority { get; set; } = new();

        // 스킵 가능한(비필수·비앵커) 일정을 전부 제외해도 여전히 하루 시간 예산을 초과하는 경우
        // true. 이 경우 남은 일정(전부 필수 또는 앵커)은 추가로 제외하지 않고 그대로 포함하며,
        // 이 플래그로만 "시간이 부족하다"는 경고를 전달한다.
        public bool ExceedsTimeBudget { get; set; }
    }
}
