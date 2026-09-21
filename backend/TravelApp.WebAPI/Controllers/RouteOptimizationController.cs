using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services.RouteOptimization;

namespace TravelApp.WebAPI.Controllers
{
    [Route("api/Trip/{tripId}/[controller]")]
    [ApiController]
    [Authorize]
    public class RouteOptimizationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly RouteOptimizationService _routeOptimizationService;

        public RouteOptimizationController(
            ApplicationDbContext context,
            RouteOptimizationService routeOptimizationService)
        {
            _context = context;
            _routeOptimizationService = routeOptimizationService;
        }

        private async Task<bool> IsTripMember(int tripId, int userId)
        {
            return await _context.TripMembers
                .AnyAsync(tm => tm.TripId == tripId && tm.UserId == userId);
        }

        // 특정 날짜의 일정을 전부 방문하는 최소 이동시간 순서를 계산한다.
        // apply=false(기본): 계산 결과만 반환. apply=true: 계산한 순서를 Schedule.Order에 실제로 반영.
        [HttpGet("{date}")]
        public async Task<IActionResult> Optimize(int tripId, DateTime date, [FromQuery] bool apply = false)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (!await IsTripMember(tripId, userId))
            {
                return Forbid();
            }

            var result = await _routeOptimizationService.OptimizeAsync(tripId, date, apply);

            if (result == null)
            {
                return NotFound("해당 날짜에 등록된 일정이 없습니다.");
            }

            return Ok(result);
        }
    }
}
