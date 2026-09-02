using System;

namespace SharedData.DTOs
{
    public class ChatSessionCreateResponseDto
    {
        public int SessionId { get; set; }

        public int TripId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
