using System;
using System.Collections.Generic;
using System.Text;

namespace SharedData.Models
{
    public class TripMember
    {
        public int Id { get; set;  }

        public int TripId { get; set; }
        public Trip? Trip { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public String Role { get; set; } = "Member";

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
