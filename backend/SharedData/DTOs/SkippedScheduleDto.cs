namespace SharedData.DTOs
{
    // 우선순위 기반 시간 예산 스킵 로직에 의해 이번 경로 최적화 결과에서 제외된 일정 하나를 나타낸다.
    // (좌표가 없어서 애초에 최적화 대상이 아니었던 일정은 RouteOptimizationResultDto의
    // SkippedSchedulesWithoutCoordinates 쪽이고, 이것과는 별개다.)
    public class SkippedScheduleDto
    {
        public int ScheduleId { get; set; }

        public string Title { get; set; } = string.Empty;

        public int Priority { get; set; }

        public string Reason { get; set; } = string.Empty;
    }
}
