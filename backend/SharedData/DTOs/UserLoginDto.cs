using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SharedData.DTOs
{
    public class UserLoginDto
    {
        [Required(ErrorMessage = "이메일을 입력해주세요.")]
        [EmailAddress(ErrorMessage = "올바른 이메일 형식이 아닙니다.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "비밀번호를 입력해주세요.")]
        public string Password { get; set; } = string.Empty;
    }
}
