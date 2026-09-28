using System.Collections.Generic;

namespace SharedData.DTOs
{
    // 숙박시설 목록 항목(TourAPI 숙박 contentTypeId=32). 정보 조회용이며 가격/잔여객실/예약 정보는 담지 않는다.
    // Latitude/Longitude는 TourAPI의 mapy/mapx이고, 값이 없거나 해석할 수 없으면 null이다
    // (클라이언트는 목록에는 표시하고 지도 마커만 생략한다).
    public class HotelListItemDto
    {
        public string ContentId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string? Tel { get; set; }

        public string? ImageUrl { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }

    // 숙박시설 상세: 목록 정보 + 개요(detailCommon2) + 기본 정보/부대시설(detailIntro2). 객실별 상세(detailInfo2)는 다루지 않는다.
    public class HotelDetailDto : HotelListItemDto
    {
        public string? Overview { get; set; }

        public List<string> Facilities { get; set; } = new();

        public string? CheckInTime { get; set; }

        public string? CheckOutTime { get; set; }
    }
}
