using SharedData.DTOs;

namespace TravelApp.WebAPI.Services.HotelInfo
{
    // 숙박시설 "정보" 조회 provider(TourAPI 4.0, 숙박 contentTypeId=32 고정). 가격·잔여객실·예약은 이 범위 밖이다 —
    // 예약은 클라이언트가 외부 예약 사이트로 이동해서 한다. 구현체는 설정("HotelInfo:Provider")으로 교체한다
    // (Mock: 고정 샘플 데이터, TourApi: 한국관광공사 TourAPI).
    public interface IHotelInfoProvider
    {
        // 한 번에 가져오는 목록 개수.
        const int PageSize = 20;

        // 지역 기반 목록(areaBasedList2). areaCode는 TourAPI 지역코드(예: 1=서울, 6=부산, 39=제주).
        Task<IReadOnlyList<HotelListItemDto>> SearchByAreaAsync(
            HotelAreaQuery area, int pageNo = 1, CancellationToken cancellationToken = default);

        // 위치 기반 목록(locationBasedList2). 가까운 순.
        Task<IReadOnlyList<HotelListItemDto>> SearchByLocationAsync(
            double latitude, double longitude, int radiusMeters, int pageNo = 1, CancellationToken cancellationToken = default);

        // 키워드 검색(searchKeyword2), 숙박만.
        Task<IReadOnlyList<HotelListItemDto>> SearchByKeywordAsync(
            string keyword, int pageNo = 1, CancellationToken cancellationToken = default);

        // 상세(detailCommon2 + detailIntro2). 없는 contentId면 null.
        Task<HotelDetailDto?> GetDetailAsync(string contentId, CancellationToken cancellationToken = default);
    }

    // 지역 검색 조건. SigunguCode는 선택(없으면 시/도 전체).
    public sealed record HotelAreaQuery(string AreaCode, string? SigunguCode = null);
}
