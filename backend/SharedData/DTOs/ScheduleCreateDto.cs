using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SharedData.DTOs
{
    public class ScheduleCreateDto
    {
        [Required(ErrorMessage = "일정 제목을 입력해주세요.")]
        [StringLength(50, ErrorMessage = "일정 제목은 50자 이하이어야 합니다.")]
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
    }
}
