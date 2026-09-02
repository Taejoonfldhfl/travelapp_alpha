using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SharedData.DTOs
{
    public class TripUpdateDto
    {
        [Required(ErrorMessage = "여행 제목을 입력해 주세요.")]
        [StringLength(50, ErrorMessage = "여행 제목은 50자 이하이어야 합니다.")]
        public string Title { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
        

    }
}
