namespace SharedData.DTOs
{
    // 장소 검색 API(주변 카테고리 검색 등)가 반환하는, 좌표가 확정된 개별 장소 하나.
    // AI 챗봇은 이 목록에 있는 장소만 추천으로 인정한다 (규칙 2).
    public class PlaceSearchResultDto
    {
        public string PlaceId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        // 지점까지 구분되는 표시용 이름. 검색 API 이름에 붙는 업종 표기('[중식]' 등)를 뗀 값이며,
        // 비어 있으면 Name을 그대로 쓴다. 같은 브랜드의 여러 지점을 LLM이 구분할 수 있게 프롬프트에 준다.
        public string CanonicalName { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        // 검색 기준점에서의 거리(m). 기준점 없이 이름으로만 찾은 결과는 null.
        public double? DistanceMeters { get; set; }

        // 영업 상태. 검색 API가 영업 정보를 주지 않으면 Unknown이며, Unknown은 Open이 아니다.
        public PlaceOperatingStatus OperatingStatus { get; set; } = PlaceOperatingStatus.Unknown;

        // 어느 검색 provider에서 온 결과인지(예: "Tmap", "Mock").
        public string Source { get; set; } = string.Empty;

        // 검색 결과 신뢰도(0~1). 검색 API가 준 관련성 순위 등을 바탕으로 계산하며, 알 수 없으면 null.
        public double? SearchConfidence { get; set; }

        // 폐업이 확인된 장소인지. true면 다른 조건과 무관하게 항상 추천에서 제외한다.
        public bool IsPermanentlyClosed { get; set; }

        // 이 장소가 영업 중이라고 마지막으로 확인된 시점. null이면 영업 정보가 없는 것이다(OperatingStatus=Unknown) —
        // 추천 후보에서 빼지는 않지만 영업 중으로 표현하지 않는다. 값이 있는데 너무 오래됐다면(3개월 초과)
        // 추천에서 제외한다 (PlaceRecommendationGrounder.IsOperatingAsOf 참고).
        public DateTime? LastConfirmedOperatingDate { get; set; }
    }
}
