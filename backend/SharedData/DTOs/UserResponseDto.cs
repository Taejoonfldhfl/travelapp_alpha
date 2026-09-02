using System;
using System.Collections.Generic;
using System.Text;

namespace SharedData.DTOs
{
    public class UserResponseDto
    {
        public string Email { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;   // JWT 토큰을 반환
    }
}
