namespace SharedData.DTOs
{
    // 장소 검색 API(주변 카테고리 검색 등)가 반환하는, 좌표가 확정된 개별 장소 하나.
    // AI 챗봇은 이 목록에 있는 장소만 추천으로 인정한다 (규칙 2).
    public class PlaceSearchResultDto
    {
        public string PlaceId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        // 폐업이 확인된 장소인지. true면 다른 조건과 무관하게 항상 추천에서 제외한다.
        public bool IsPermanentlyClosed { get; set; }

        // 이 장소가 영업 중이라고 마지막으로 확인된 시점. null이면 "방금 이 검색 결과로
        // 확인됨"으로 간주해 최신 정보로 취급한다. 값이 있는데 너무 오래됐다면(3개월 초과)
        // 추천에서 제외한다 (PlaceRecommendationGrounder.IsOperatingAsOf 참고).
        public DateTime? LastConfirmedOperatingDate { get; set; }
    }
}
