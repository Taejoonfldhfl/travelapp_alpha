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

        // 같은 날짜에서 일정 정렬용
        public int Order { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
