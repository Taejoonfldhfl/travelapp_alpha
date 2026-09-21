using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SharedData.DTOs
{
    public class ScheduleUpdateDto
    {
        [Required(ErrorMessage = "일정 제목을 입력해주세요.")]
        [StringLength(50)]
        public string Title { get; set; } = string.Empty;

        [StringLength(100)]
        public string PlaceName { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        public int Order { get; set; }

        [Range(-90, 90)]
        public double? Latitude { get; set; }

        [Range(-180, 180)]
        public double? Longitude { get; set; }

        // 다음 단계(우선순위 기반 선택적 방문)를 위한 필드. 이번 단계 로직에서는 사용하지 않는다.
        public int Priority { get; set; } = 0;

        public bool IsEssential { get; set; } = true;
    }
}
