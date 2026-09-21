using System;
using System.Collections.Generic;
using System.Text;

namespace SharedData.Models
{
    public class Schedule
    {
        public int Id { get; set; }

        // 어떤 여행의 일정인지
        public int TripId { get; set; }
        public Trip? Trip { get; set; }

        // 일정 제목
        public string Title { get; set; } = string.Empty;

        // 장소
        public string PlaceName { get; set; } = string.Empty;

        // 설명, 메모
        public string Description { get; set; } = string.Empty;

        // 시작 시간
        public DateTime StartTime { get; set; }

        // 종료 시간
        public DateTime EndTime { get; set; }

        // 같은 날짜에서 일정 정렬용 (경로 최적화 결과도 이 필드에 반영됨)
        public int Order { get; set; }

        // 장소의 실제 위도/경도. 경로 최적화 대상이 되려면 반드시 값이 있어야 한다.
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // 우선순위/필수방문 여부: 다음 단계(선택적 방문 로직)를 위해 필드만 마련해두고
        // 이번 단계의 경로 최적화 로직(순수 TSP)에서는 참조하지 않는다.
        public int Priority { get; set; } = 0;
        public bool IsEssential { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
