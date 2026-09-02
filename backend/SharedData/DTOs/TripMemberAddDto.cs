using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SharedData.DTOs
{
    public class TripMemberAddDto
    {
        [Required(ErrorMessage = "이메일을 입력해주세요.")]
        [EmailAddress(ErrorMessage = "올바른 이메일 형식이 아닙니다.")]
        public string Email { get; set; } = string.Empty;
    }
}
