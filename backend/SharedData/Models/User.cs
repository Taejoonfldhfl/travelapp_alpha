using System;
using System.Collections.Generic;
using System.Text;

namespace SharedData.Models
{
    public class User
    {
        public int Id { get; set; }     // 유저 식별자
        public string Email { get; set; } = string.Empty;       // 유저 이메일
        public string PasswordHash { get; set; } = string.Empty;        // 비밀번호 해시값
        public string Nickname { get; set; } = string.Empty;            // 사용자명
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;      // 가입시간

        public List<Trip> Trips { get; set; } = new();      // 이 사용자가 만든 여행 목록

        public List<TripMember> TripMemberships { get; set; } = new(); // 이 사용자가 참여한 여행 목록
    }
}
