using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedData.DTOs;
using TravelApp.WebAPI.Services.HotelInfo;

namespace TravelApp.WebAPI.Controllers
{
    // 숙박시설 정보 조회(TourAPI). 가격·잔여객실·예약은 다루지 않는다 — 예약은 앱이 외부 예약 사이트로 넘긴다.
    // 여행(Trip)과 무관한 공용 정보라 Trip 하위 라우트가 아니라 별도 컨트롤러로 둔다.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class HotelInfoController : ControllerBase
    {
        public const int DefaultRadiusMeters = 2000;

        private readonly IHotelInfoProvider _hotelInfoProvider;
        private readonly ILogger<HotelInfoController> _logger;

        public HotelInfoController(IHotelInfoProvider hotelInfoProvider, ILogger<HotelInfoController> logger)
        {
            _hotelInfoProvider = hotelInfoProvider;
            _logger = logger;
        }

        // GET api/HotelInfo/search?keyword=...            키워드 검색
        // GET api/HotelInfo/search?areaCode=1&sigunguCode=23  지역 검색(sigunguCode 선택)
        // GET api/HotelInfo/search?lat=..&lng=..&radius=2000  위치 검색(radius 선택, 1~20000m)
        // 검색 방식은 셋 중 정확히 하나만 줘야 한다.
        [HttpGet("search")]
        public async Task<ActionResult<List<HotelListItemDto>>> Search(
            [FromQuery] string? keyword,
            [FromQuery] string? areaCode,
            [FromQuery] string? sigunguCode,
            [FromQuery] double? lat,
            [FromQuery] double? lng,
            [FromQuery] int? radius,
            [FromQuery] int page = 1)
        {
            bool hasKeyword = !string.IsNullOrWhiteSpace(keyword);
            bool hasArea = !string.IsNullOrWhiteSpace(areaCode);
            bool hasLocation = lat.HasValue || lng.HasValue;

            if ((hasKeyword ? 1 : 0) + (hasArea ? 1 : 0) + (hasLocation ? 1 : 0) != 1)
            {
                return BadRequest("keyword, areaCode, lat/lng 중 하나만 지정하세요.");
            }

            if (hasLocation && (lat is not (>= -90 and <= 90) || lng is not (>= -180 and <= 180)))
            {
                return BadRequest("위치 검색에는 올바른 lat(-90~90)과 lng(-180~180)가 모두 필요합니다.");
            }

            if (radius is <= 0 or > TourApiHotelInfoProvider.MaxRadiusMeters)
            {
                return BadRequest($"radius는 1~{TourApiHotelInfoProvider.MaxRadiusMeters}m 사이여야 합니다.");
            }

            if (page < 1)
            {
                return BadRequest("page는 1 이상이어야 합니다.");
            }

            var cancellationToken = HttpContext.RequestAborted;

            try
            {
                IReadOnlyList<HotelListItemDto> results =
                    hasKeyword ? await _hotelInfoProvider.SearchByKeywordAsync(keyword!.Trim(), page, cancellationToken)
                    : hasArea ? await _hotelInfoProvider.SearchByAreaAsync(new HotelAreaQuery(areaCode!.Trim(), NullIfBlank(sigunguCode)), page, cancellationToken)
                    : await _hotelInfoProvider.SearchByLocationAsync(lat!.Value, lng!.Value, radius ?? DefaultRadiusMeters, page, cancellationToken);

                return Ok(results.ToList());
            }
            catch (Exception ex) when (IsProviderFailure(ex, cancellationToken))
            {
                return ProviderFailure(ex);
            }
        }

        // GET api/HotelInfo/{contentId}
        [HttpGet("{contentId}")]
        public async Task<ActionResult<HotelDetailDto>> GetDetail(string contentId)
        {
            var cancellationToken = HttpContext.RequestAborted;

            try
            {
                var detail = await _hotelInfoProvider.GetDetailAsync(contentId.Trim(), cancellationToken);
                return detail == null ? NotFound("숙박시설 정보를 찾을 수 없습니다.") : Ok(detail);
            }
            catch (Exception ex) when (IsProviderFailure(ex, cancellationToken))
            {
                return ProviderFailure(ex);
            }
        }

        private ObjectResult ProviderFailure(Exception ex)
        {
            _logger.LogWarning("숙박시설 정보 조회 실패: {Error}", ex.Message);
            return StatusCode(502, "숙박시설 정보를 가져오지 못했습니다. 잠시 후 다시 시도해 주세요.");
        }

        // 외부 API 실패(네트워크/HTTP 오류/응답 형식 오류/타임아웃). 호출한 쪽의 취소는 실패로 삼키지 않는다.
        private static bool IsProviderFailure(Exception ex, CancellationToken cancellationToken) =>
            ex is InvalidOperationException or HttpRequestException or JsonException ||
            (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
