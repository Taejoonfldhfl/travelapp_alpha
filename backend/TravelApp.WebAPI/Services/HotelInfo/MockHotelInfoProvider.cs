using SharedData.DTOs;
using TravelApp.WebAPI.Services.RouteOptimization;

namespace TravelApp.WebAPI.Services.HotelInfo
{
    // TourAPI 인증키가 없을 때(기본값)와 테스트에서 쓰는 고정 샘플 데이터. 실제 숙소가 아니라는 게 드러나도록
    // 이름에 "샘플"을 붙였다. 좌표가 없는 항목을 하나 넣어 "목록에는 나오고 지도 마커만 빠지는" 경우도 재현한다.
    public class MockHotelInfoProvider : IHotelInfoProvider
    {
        private sealed record Sample(
            string ContentId, string Name, string Address, string? Tel, string AreaCode, string SigunguCode,
            double? Latitude, double? Longitude, string Overview, string[] Facilities);

        // 지역코드: 1=서울(23=종로구, 24=중구, 1=강남구), 6=부산(16=해운대구), 39=제주(3=서귀포시).
        private static readonly Sample[] Samples =
        [
            new("mock-1001", "샘플 광화문 호텔", "서울특별시 종로구 세종대로 170", "02-000-1001", "1", "23",
                37.5720, 126.9769, "광화문광장과 경복궁에 걸어서 갈 수 있는 도심 호텔(샘플 데이터).", ["피트니스센터", "비즈니스센터", "주차 가능"]),
            new("mock-1002", "샘플 명동 스테이", "서울특별시 중구 명동길 14", "02-000-1002", "1", "24",
                37.5637, 126.9838, "명동 쇼핑거리 중심의 숙소(샘플 데이터).", ["공용 PC실"]),
            new("mock-1003", "샘플 강남 비즈니스 호텔", "서울특별시 강남구 테헤란로 152", "02-000-1003", "1", "1",
                37.5000, 127.0364, "강남역과 역삼역 사이의 비즈니스 호텔(샘플 데이터).", ["세미나실", "사우나"]),
            new("mock-1004", "샘플 종로 한옥 게스트하우스", "서울특별시 종로구 북촌로 31", null, "1", "23",
                null, null, "좌표가 등록되지 않은 숙소 예시(샘플 데이터) — 목록에는 보이고 지도에는 표시되지 않는다.", []),
            new("mock-2001", "샘플 해운대 오션 호텔", "부산광역시 해운대구 해운대해변로 264", "051-000-2001", "6", "16",
                35.1587, 129.1604, "해운대 해수욕장 앞 숙소(샘플 데이터).", ["식음료장", "주차 가능"]),
            new("mock-3001", "샘플 중문 리조트", "제주특별자치도 서귀포시 중문관광로 72", "064-000-3001", "39", "3",
                33.2489, 126.4116, "중문관광단지 안 리조트(샘플 데이터).", ["바비큐장", "스포츠 시설"]),
        ];

        public Task<IReadOnlyList<HotelListItemDto>> SearchByAreaAsync(
            HotelAreaQuery area, int pageNo = 1, CancellationToken cancellationToken = default) =>
            Page(Samples.Where(s => s.AreaCode == area.AreaCode &&
                                    (string.IsNullOrWhiteSpace(area.SigunguCode) || s.SigunguCode == area.SigunguCode)), pageNo);

        public Task<IReadOnlyList<HotelListItemDto>> SearchByLocationAsync(
            double latitude, double longitude, int radiusMeters, int pageNo = 1, CancellationToken cancellationToken = default) =>
            Page(Samples
                .Where(s => s.Latitude != null && s.Longitude != null)
                .Select(s => (Sample: s, Distance: HaversineTravelTimeProvider.HaversineDistanceMeters(
                    latitude, longitude, s.Latitude!.Value, s.Longitude!.Value)))
                .Where(x => x.Distance <= radiusMeters)
                .OrderBy(x => x.Distance)
                .Select(x => x.Sample), pageNo);

        public Task<IReadOnlyList<HotelListItemDto>> SearchByKeywordAsync(
            string keyword, int pageNo = 1, CancellationToken cancellationToken = default)
        {
            string normalized = Normalize(keyword);

            return Page(normalized.Length == 0
                ? []
                : Samples.Where(s => Normalize(s.Name).Contains(normalized, StringComparison.Ordinal) ||
                                     Normalize(s.Address).Contains(normalized, StringComparison.Ordinal)), pageNo);
        }

        public Task<HotelDetailDto?> GetDetailAsync(string contentId, CancellationToken cancellationToken = default)
        {
            var sample = Samples.FirstOrDefault(s => s.ContentId == contentId);
            if (sample == null)
            {
                return Task.FromResult<HotelDetailDto?>(null);
            }

            var detail = new HotelDetailDto
            {
                Overview = sample.Overview,
                Facilities = sample.Facilities.ToList(),
                CheckInTime = "15:00",
                CheckOutTime = "11:00"
            };
            Fill(detail, sample);
            return Task.FromResult<HotelDetailDto?>(detail);
        }

        private static Task<IReadOnlyList<HotelListItemDto>> Page(IEnumerable<Sample> samples, int pageNo) =>
            Task.FromResult<IReadOnlyList<HotelListItemDto>>(samples
                .Skip((Math.Max(1, pageNo) - 1) * IHotelInfoProvider.PageSize)
                .Take(IHotelInfoProvider.PageSize)
                .Select(s => Fill(new HotelListItemDto(), s))
                .ToList());

        private static T Fill<T>(T dto, Sample sample) where T : HotelListItemDto
        {
            dto.ContentId = sample.ContentId;
            dto.Name = sample.Name;
            dto.Address = sample.Address;
            dto.Tel = sample.Tel;
            dto.ImageUrl = null; // 샘플 데이터에는 사진이 없다(클라이언트 플레이스홀더).
            dto.Latitude = sample.Latitude;
            dto.Longitude = sample.Longitude;
            return dto;
        }

        private static string Normalize(string value) => value.Replace(" ", string.Empty).Trim().ToLowerInvariant();
    }
}
