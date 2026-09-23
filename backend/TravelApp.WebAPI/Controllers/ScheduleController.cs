using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedData.DTOs;
using SharedData.Models;
using System.Security.Claims;
using TravelApp.WebAPI.Data;

namespace TravelApp.WebAPI.Controllers
{
    [Route("api/Trip/{tripId}/[controller]")]
    [ApiController]
    [Authorize]
    public class ScheduleController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ScheduleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 해당 여행의 맴버인지 확인
        private async Task<bool> IsTripMember(int tripId, int userId)
        {
            return await _context.TripMembers
                .AnyAsync(tm =>
                    tm.TripId == tripId &&
                    tm.UserId == userId
                );
        }

        // 1. 일정 생성
        [HttpPost]
        public async Task<ActionResult<ScheduleResponseDto>> CreateSchedule(int tripId, ScheduleCreateDto request)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            if (!await IsTripMember(tripId, userId))
            {
                return Forbid();
            }

            if (request.StartTime > request.EndTime)
            {
                return BadRequest("일정 종료 시간은 시작 시간보다 빠를 수 없습니다.");
            }

            var trip = await _context.Trips.FindAsync(tripId);

            if (trip == null)
            {
                return NotFound("여행을 찾을 수 없습니다.");
            }

            // 여행 기간 밖의 일정 방지
            if (request.StartTime.Date < trip.StartDate.Date ||
                request.EndTime.Date > trip.EndDate.Date)
            {
                return BadRequest("일정은 여행 기간 내에 등록해야 합니다.");
            }

            var overlapping = await _context.FindOverlappingSchedulesAsync(
                tripId, request.StartTime, request.EndTime, request.IsHotelCheckIn);

            if (overlapping.Count > 0)
            {
                return BadRequest(BuildOverlapMessage(overlapping));
            }

            var schedule = new Schedule
            {
                TripId = tripId,
                Title = request.Title,
                PlaceName = request.PlaceName,
                Description = request.Description,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Order = request.Order,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Priority = request.Priority,
                IsEssential = request.IsEssential,
                IsHotelCheckIn = request.IsHotelCheckIn
            };

            _context.Schedules.Add(schedule);
            await _context.SaveChangesAsync();

            return Ok(ToResponse(schedule));
        }

        // 2. 여행 전체 일정 조회
        [HttpGet]
        public async Task<ActionResult<List<ScheduleResponseDto>>> GetSchedules(
            int tripId)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            if (!await IsTripMember(tripId, userId))
            {
                return Forbid();
            }

            // 날짜(그룹)가 우선, 같은 날짜 안에서는 Order가 우선(경로 최적화 결과 반영),
            // Order가 같으면(기본값 0 등 미최적화 상태) 기존처럼 StartTime으로 정렬한다.
            var schedules = await _context.Schedules
                .Where(s => s.TripId == tripId)
                .OrderBy(s => s.StartTime.Date)
                .ThenBy(s => s.Order)
                .ThenBy(s => s.StartTime)
                .Select(s => new ScheduleResponseDto
                {
                    Id = s.Id,
                    TripId = s.TripId,
                    Title = s.Title,
                    PlaceName = s.PlaceName,
                    Description = s.Description,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Order = s.Order,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    Priority = s.Priority,
                    IsEssential = s.IsEssential,
                    IsHotelCheckIn = s.IsHotelCheckIn,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            return Ok(schedules);
        }

        // 3. 특정 일정 조회
        [HttpGet("{scheduleId}")]
        public async Task<ActionResult<ScheduleResponseDto>> GetSchedule(
            int tripId,
            int scheduleId)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            if (!await IsTripMember(tripId, userId))
            {
                return Forbid();
            }

            var schedule = await _context.Schedules
                .FirstOrDefaultAsync(s =>
                    s.Id == scheduleId &&
                    s.TripId == tripId);

            if (schedule == null)
            {
                return NotFound("일정을 찾을 수 없습니다.");
            }

            return Ok(ToResponse(schedule));
        }

        // 4. 일정 수정
        [HttpPut("{scheduleId}")]
        public async Task<ActionResult<ScheduleResponseDto>> UpdateSchedule(
            int tripId,
            int scheduleId,
            ScheduleUpdateDto request)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            if (!await IsTripMember(tripId, userId))
            {
                return Forbid();
            }

            if (request.StartTime > request.EndTime)
            {
                return BadRequest("일정 종료 시간은 시작 시간보다 빠를 수 없습니다.");
            }

            var trip = await _context.Trips.FindAsync(tripId);

            if (trip == null)
            {
                return NotFound("여행을 찾을 수 없습니다.");
            }

            if (request.StartTime.Date < trip.StartDate.Date ||
                request.EndTime.Date > trip.EndDate.Date)
            {
                return BadRequest("일정은 여행 기간 내에 등록해야 합니다.");
            }

            var schedule = await _context.Schedules
                .FirstOrDefaultAsync(s =>
                    s.Id == scheduleId &&
                    s.TripId == tripId);

            if (schedule == null)
            {
                return NotFound("일정을 찾을 수 없습니다.");
            }

            var overlapping = await _context.FindOverlappingSchedulesAsync(
                tripId, request.StartTime, request.EndTime, request.IsHotelCheckIn, excludeScheduleId: scheduleId);

            if (overlapping.Count > 0)
            {
                return BadRequest(BuildOverlapMessage(overlapping));
            }

            schedule.Title = request.Title;
            schedule.PlaceName = request.PlaceName;
            schedule.Description = request.Description;
            schedule.StartTime = request.StartTime;
            schedule.EndTime = request.EndTime;
            schedule.Order = request.Order;
            schedule.Latitude = request.Latitude;
            schedule.Longitude = request.Longitude;
            schedule.Priority = request.Priority;
            schedule.IsEssential = request.IsEssential;
            schedule.IsHotelCheckIn = request.IsHotelCheckIn;

            await _context.SaveChangesAsync();

            return Ok(ToResponse(schedule));
        }

        // 5. 일정 삭제
        [HttpDelete("{scheduleId}")]
        public async Task<IActionResult> DeleteSchedule(
            int tripId,
            int scheduleId)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            if (!await IsTripMember(tripId, userId))
            {
                return Forbid();
            }

            var schedule = await _context.Schedules
                .FirstOrDefaultAsync(s =>
                    s.Id == scheduleId &&
                    s.TripId == tripId);

            if (schedule == null)
            {
                return NotFound("일정을 찾을 수 없습니다.");
            }

            _context.Schedules.Remove(schedule);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "일정이 삭제되었습니다."
            });
        }

        // 앱은 400 본문 중 평문만 사용자에게 보여주므로(최대 200자) JSON이 아닌 문장으로 돌려준다.
        // 겹치는 일정이 많으면 앞의 몇 개만 적고 나머지는 개수로 줄인다.
        private const int MaxOverlapsInMessage = 3;

        private static string BuildOverlapMessage(List<Schedule> overlapping)
        {
            var listed = overlapping
                .Take(MaxOverlapsInMessage)
                .Select(s => $"'{s.Title}' ({s.StartTime:MM-dd HH:mm}~{s.EndTime:MM-dd HH:mm})");

            string message = "다른 일정과 시간이 겹칩니다: " + string.Join(", ", listed);

            if (overlapping.Count > MaxOverlapsInMessage)
            {
                message += $" 외 {overlapping.Count - MaxOverlapsInMessage}건";
            }

            return message;
        }

        private static ScheduleResponseDto ToResponse(Schedule schedule)
        {
            return new ScheduleResponseDto
            {
                Id = schedule.Id,
                TripId = schedule.TripId,
                Title = schedule.Title,
                PlaceName = schedule.PlaceName,
                Description = schedule.Description,
                StartTime = schedule.StartTime,
                EndTime = schedule.EndTime,
                Order = schedule.Order,
                Latitude = schedule.Latitude,
                Longitude = schedule.Longitude,
                Priority = schedule.Priority,
                IsEssential = schedule.IsEssential,
                IsHotelCheckIn = schedule.IsHotelCheckIn,
                CreatedAt = schedule.CreatedAt
            };
        }
    }
}
