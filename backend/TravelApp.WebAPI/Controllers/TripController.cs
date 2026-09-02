using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedData.DTOs;
using SharedData.Models;
using System.Security.Claims;
using TravelApp.WebAPI.Data;

namespace TravelApp.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TripController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TripController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. 여행 생성
        [HttpPost]
        public async Task<ActionResult<TripResponseDto>> CreateTrip(TripCreateDto request)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            if (request.StartDate > request.EndDate)
            {
                return BadRequest("여행 종료일은 시작일보다 빠를 수 없습니다.");
            }

            var trip = new Trip
            {
                Title = request.Title,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                OwnerId = userId
            };

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            var ownerMember = new TripMember
            {
                TripId = trip.Id,
                UserId = userId,
                Role = "Owner"
            };

            _context.TripMembers.Add(ownerMember);
            await _context.SaveChangesAsync();

            return Ok(new TripResponseDto
            {
                Id = trip.Id,
                Title = trip.Title,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                CreatedAt = trip.CreatedAt
            });
        }

        // 2. 내가 만든 여행 목록 조회
        [HttpGet]
        public async Task<ActionResult<List<TripResponseDto>>> GetTrips()
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            var trips = await _context.TripMembers
                .Where(tm => tm.UserId == userId)
                .Select(tm => new TripResponseDto
                {
                    Id = tm.Trip!.Id,
                    Title = tm.Trip.Title,
                    StartDate = tm.Trip.StartDate,
                    EndDate = tm.Trip.EndDate,
                    CreatedAt = tm.Trip.CreatedAt
                })
                .ToListAsync();

            return Ok(trips);
        }

        // 3. 특정 여행 조회
        [HttpGet("{id}")]
        public async Task<ActionResult<TripResponseDto>> GetTrip(int id)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            bool isMember = await _context.TripMembers
                .AnyAsync(tm =>
                    tm.TripId == id &&
                    tm.UserId == userId
                );

            if (!isMember)
            {
                return NotFound("여행을 찾을 수 없습니다.");
            }

            var trip = await _context.Trips
                .FirstOrDefaultAsync(
                    t => t.Id == id
                );

            if (trip == null)
            {
                return NotFound("여행을 찾을 수 업습니다.");
            }

            return Ok(new TripResponseDto
            {
                Id = trip.Id,
                Title = trip.Title,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                CreatedAt = trip.CreatedAt
            });
        }

        // 4. 여행 수정
        [HttpPut("id")]
        public async Task<ActionResult<TripResponseDto>> UpdateTrip(int id,TripUpdateDto request)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            if (request.StartDate > request.EndDate)
            {
                return BadRequest("여행 종료일은 시작일보다 빠를 수 없습니다.");
            }

            var trip = await _context.Trips
                .FirstOrDefaultAsync(
                    t => t.Id == id && t.OwnerId == userId
                );
            
            if (trip == null)
            {
                return NotFound("여행을 찾을 수 업습니다.");
            }

            trip.Title = request.Title;
            trip.StartDate = request.StartDate;
            trip.EndDate = request.EndDate;

            await _context.SaveChangesAsync();

            return Ok(new TripResponseDto
            {
                Id = trip.Id,
                Title = trip.Title,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                CreatedAt = trip.CreatedAt
            });
        }

        // 5. 여행 삭제
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTrip(int id)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            var trip = await _context.Trips
                .FirstOrDefaultAsync(
                    t => t.Id == id && t.OwnerId == userId
                );

            if (trip == null)
            {
                return NotFound("여행을 찾을 수 업습니다.");
            }

            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "여행이 삭제되었습니다."
            });
        }

        // 6. 여행 멤버 추가
        [HttpPost("{id}/members")]
        public async Task<IActionResult> AddMember(int id, TripMemberAddDto request)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            // 여행 조회
            var trip = await _context.Trips
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null)
            {
                return NotFound("여행을 찾을 수 업습니다.");
            }

            // 여행 생성자만 맴버 추가 가능
            if (trip.OwnerId != userId)
            {
                return Forbid();
            }

            // 이메일로 사용자 조회
            var targetUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (targetUser == null)
            {
                return NotFound("해당 이메일의 사용자를 찾을 수 없습니다.");
            }

            // 이미 멤버인지 확인
            bool alreadyMember = await _context.TripMembers
                .AnyAsync(tm =>
                    tm.TripId == id &&
                    tm.UserId == targetUser.Id);

            if (alreadyMember)
            {
                return BadRequest("이미 여행에 참여 중인 사용자입니다.");
            }

            var tripMember = new TripMember
            {
                TripId = id,
                UserId = targetUser.Id,
                Role = "Member"
            };

            _context.TripMembers.Add(tripMember);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "여행 맴버가 추가되었습니다"
            });
        }

        // 7. 여행 멤버 조회
        [HttpGet("{id}/members")]
        public async Task<ActionResult<List<TripMemberResponseDto>>> GetMembers(int id)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            // 요청한 사용자가 해당 여행 맴버인지 확인
            bool isMember = await _context.TripMembers
                .AnyAsync(tm =>
                    tm.TripId == id &&
                    tm.UserId == userId
                );

            if (!isMember)
            {
                return Forbid();
            }

            var members = await _context.TripMembers
                .Where(tm => tm.TripId == id)
                .Select(tm => new TripMemberResponseDto
                {
                    UserId = tm.UserId,
                    Email = tm.User!.Email,
                    NickName = tm.User.Nickname,
                    Role = tm.Role
                })
                .ToListAsync();

            return Ok(members);
        }
    }
}
