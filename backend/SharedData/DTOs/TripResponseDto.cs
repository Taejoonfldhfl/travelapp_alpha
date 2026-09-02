using System;
using System.Collections.Generic;
using System.Text;

namespace SharedData.DTOs
{
    public class TripResponseDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        
        public DateTime EndDate { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
