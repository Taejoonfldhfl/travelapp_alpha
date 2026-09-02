using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SharedData.DTOs
{
    public class UserRegisterDto
    {
        [Required(ErrorMessage = "이메일을 입력해주세요.")]
        [EmailAddress(ErrorMessage = "올바른 이메일 형식이 아닙니다.")]    
        public string Email { get; set; } = string.Empty;       // 필수값, 이메일 형식이어야 함.

        [Required(ErrorMessage = "비밀번호를 입력해주세요.")]
        [MinLength(6, ErrorMessage = "비밀번호는 최소 6자 이상이어야 합니다.")]    
        public string Password { get; set; } = string.Empty;    //  필수값, 비밀번호는 최소 6자리 이상이어야 함.

        [Required(ErrorMessage = "닉네임을 입력해주세요.")]
        [StringLength(15, MinimumLength = 2, ErrorMessage = "닉네임은 2자 이상 15자 이하이어야 합니다.")]
        public string Nickname { get; set; } = string.Empty;    // 필수값, 닉네임은 2자 이상 15자 이하이어야 함.
    }
}
