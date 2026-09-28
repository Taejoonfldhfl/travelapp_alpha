using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SharedData.DTOs;
using TravelApp.WebAPI.Controllers;
using TravelApp.WebAPI.Services.HotelInfo;

namespace TravelApp.WebAPI.Tests;

// 숙박시설 정보 API: 검색 방식 선택/검증, 상세 404, 외부 API 실패 시 502.
public class HotelInfoControllerTests
{
    // 어떤 검색 메서드가 어떤 인자로 불렸는지 기록한다. Throw를 주면 외부 API 실패를 재현한다.
    private sealed class RecordingProvider : IHotelInfoProvider
    {
        private readonly MockHotelInfoProvider _inner = new();

        public List<string> Calls { get; } = new();

        public Exception? Throw { get; init; }

        public Task<IReadOnlyList<HotelListItemDto>> SearchByAreaAsync(HotelAreaQuery area, int pageNo = 1, CancellationToken cancellationToken = default)
        {
            Calls.Add($"area:{area.AreaCode}:{area.SigunguCode ?? "-"}:p{pageNo}");
            ThrowIfSet();
            return _inner.SearchByAreaAsync(area, pageNo, cancellationToken);
        }

        public Task<IReadOnlyList<HotelListItemDto>> SearchByLocationAsync(double latitude, double longitude, int radiusMeters, int pageNo = 1, CancellationToken cancellationToken = default)
        {
            Calls.Add($"location:{latitude}:{longitude}:{radiusMeters}");
            ThrowIfSet();
            return _inner.SearchByLocationAsync(latitude, longitude, radiusMeters, pageNo, cancellationToken);
        }

        public Task<IReadOnlyList<HotelListItemDto>> SearchByKeywordAsync(string keyword, int pageNo = 1, CancellationToken cancellationToken = default)
        {
            Calls.Add($"keyword:{keyword}");
            ThrowIfSet();
            return _inner.SearchByKeywordAsync(keyword, pageNo, cancellationToken);
        }

        public Task<HotelDetailDto?> GetDetailAsync(string contentId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"detail:{contentId}");
            ThrowIfSet();
            return _inner.GetDetailAsync(contentId, cancellationToken);
        }

        private void ThrowIfSet()
        {
            if (Throw != null)
            {
                throw Throw;
            }
        }
    }

    private static HotelInfoController Controller(IHotelInfoProvider provider) =>
        new(provider, NullLogger<HotelInfoController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static List<HotelListItemDto> OkList(ActionResult<List<HotelListItemDto>> result) =>
        Assert.IsType<List<HotelListItemDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Fact]
    public void 다른_컨트롤러처럼_인증이_필요하다()
    {
        Assert.NotNull(typeof(HotelInfoController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task 키워드_검색()
    {
        var provider = new RecordingProvider();

        var hotels = OkList(await Controller(provider).Search(" 광화문 ", null, null, null, null, null));

        Assert.Equal(["keyword:광화문"], provider.Calls);
        Assert.Equal("샘플 광화문 호텔", Assert.Single(hotels).Name);
    }

    [Fact]
    public async Task 지역_검색은_시군구코드와_페이지를_넘긴다()
    {
        var provider = new RecordingProvider();

        var hotels = OkList(await Controller(provider).Search(null, "1", "23", null, null, null, page: 1));

        Assert.Equal(["area:1:23:p1"], provider.Calls);
        Assert.Equal(2, hotels.Count);
    }

    [Fact]
    public async Task 위치_검색은_반경을_주지_않으면_2km다()
    {
        var provider = new RecordingProvider();

        var hotels = OkList(await Controller(provider).Search(null, null, null, 37.5759, 126.9769, null));

        Assert.Equal([$"location:37.5759:126.9769:{HotelInfoController.DefaultRadiusMeters}"], provider.Calls);
        Assert.Equal(2, hotels.Count);
    }

    [Fact]
    public async Task 좌표없는_숙소도_목록에는_좌표_null로_포함된다()
    {
        var hotels = OkList(await Controller(new RecordingProvider()).Search("한옥", null, null, null, null, null));

        var hanok = Assert.Single(hotels);
        Assert.Null(hanok.Latitude);
        Assert.Null(hanok.Longitude);
    }

    [Theory]
    [InlineData(null, null, null, null)]          // 검색 조건 없음
    [InlineData("호텔", "1", null, null)]          // 두 가지를 동시에
    [InlineData("호텔", null, 37.5, 126.9)]
    [InlineData(null, null, 37.5, null)]         // lng 없음
    [InlineData(null, null, null, 126.9)]        // lat 없음
    [InlineData(null, null, 91.0, 126.9)]        // 범위 밖
    [InlineData(null, null, 37.5, 181.0)]
    [InlineData("   ", "  ", null, null)]        // 공백은 없는 것과 같다
    public async Task 검색조건은_정확히_하나여야_한다(string? keyword, string? areaCode, double? lat, double? lng)
    {
        var provider = new RecordingProvider();

        var result = await Controller(provider).Search(keyword, areaCode, null, lat, lng, null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(provider.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(20001)]
    public async Task 반경이_범위를_벗어나면_400이다(int radius)
    {
        var result = await Controller(new RecordingProvider()).Search(null, null, null, 37.5, 126.9, radius);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task 페이지가_1보다_작으면_400이다()
    {
        var result = await Controller(new RecordingProvider()).Search("호텔", null, null, null, null, null, page: 0);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task 상세_조회()
    {
        var result = await Controller(new RecordingProvider()).GetDetail("mock-2001");

        var detail = Assert.IsType<HotelDetailDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("샘플 해운대 오션 호텔", detail.Name);
        Assert.NotEmpty(detail.Facilities);
    }

    [Fact]
    public async Task 없는_숙소_상세는_404다()
    {
        var result = await Controller(new RecordingProvider()).GetDetail("nope");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task 외부_API가_실패하면_검색과_상세_모두_502다()
    {
        var provider = new RecordingProvider { Throw = new InvalidOperationException("TourAPI 오류 응답입니다: 30 SERVICE_KEY_IS_NOT_REGISTERED_ERROR") };

        var search = await Controller(provider).Search("호텔", null, null, null, null, null);
        var detail = await Controller(provider).GetDetail("mock-1001");

        Assert.Equal(502, Assert.IsType<ObjectResult>(search.Result).StatusCode);
        Assert.Equal(502, Assert.IsType<ObjectResult>(detail.Result).StatusCode);
        // 사용자에게는 내부 오류 메시지(인증키 문제 등)를 그대로 노출하지 않는다.
        Assert.DoesNotContain("SERVICE_KEY", Assert.IsType<ObjectResult>(search.Result).Value!.ToString());
    }
}
