using System;
using System.Collections.Generic;
using System.Text;

namespace SharedData.DTOs
{
    public class ScheduleResponseDto
    {
        public int Id { get; set; }

        public int TripId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string PlaceName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public int Order { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public int Priority { get; set; }

        public bool IsEssential { get; set; }

        public bool IsHotelCheckIn { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
