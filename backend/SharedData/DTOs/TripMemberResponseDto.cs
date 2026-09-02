using System;
using System.Collections.Generic;
using System.Text;

namespace SharedData.DTOs
{
    public class TripMemberResponseDto
    {
        public int UserId { get; set; }

        public string Email { get; set; } = string.Empty;

        public string NickName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
    }
}
