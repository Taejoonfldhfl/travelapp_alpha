using System;
using System.Collections.Generic;
using System.Text;

namespace SharedData.Models
{
    public class Trip
    {
        public int Id { get; set;  }

        // 여행 제목
        public string Title { get; set; } = string.Empty;

        // 여행 시작일
        public DateTime StartDate { get; set; }

        // 여행 종료일
        public DateTime EndDate { get; set; }

        // 여행을 만든 사용자 ID
        public int OwnerId { get; set; }

        // 여행을 만든 사용자
        public User? Owner { get; set; }

        // 생성 시간
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 여행 맴버 목록
        public List<TripMember> Members { get; set; } = new();

        // 여행 일정 목록
        public List<Schedule> Schedules { get; set; } = new();
    }
}
