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
        // dayStartTime/dayEndTime(선택, "HH:mm"): 그날 활동에 배정 가능한 시간 범위. 필요한 이동시간+
        // 활동시간이 이 범위를 넘을 것으로 예상되면 우선순위 낮은(비필수) 일정부터 제외된다. 둘 다
        // 주지 않으면 그날 등록된 일정들의 시작~종료 범위를 기본 예산으로 쓴다.
        [HttpGet("{date}")]
        public async Task<IActionResult> Optimize(
            int tripId,
            DateTime date,
            [FromQuery] bool apply = false,
            [FromQuery] TimeSpan? dayStartTime = null,
            [FromQuery] TimeSpan? dayEndTime = null)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (!await IsTripMember(tripId, userId))
            {
                return Forbid();
            }

            var result = await _routeOptimizationService.OptimizeAsync(tripId, date, apply, dayStartTime, dayEndTime);

            if (result == null)
            {
                return NotFound("해당 날짜에 등록된 일정이 없습니다.");
            }

            return Ok(result);
        }
    }
}
