using TravelApp.WebAPI.Services.PlaceSearch;

namespace TravelApp.WebAPI.Services.QueryAnalysis
{
    // 사용자 질문의 검색 의도. 검색 기준점을 무엇으로 삼을지가 이 값으로 정해진다.
    public enum TravelQueryIntent
    {
        CurrentLocation,        // "근처 맛집", "내 주변 카페" — 현재 GPS 기준
        NamedLocation,          // "강남 맛집", "경복궁 근처 한식집" — 사용자가 말한 지역/랜드마크 기준
        SpecificPlace,          // "스타벅스 강남역점 알려줘" — 특정 가게/지점
        GeneralRecommendation,  // "이번 여행 맛집 추천" — 여행 일정/지역 기준
        NonRecommendation       // "오늘 일정 알려줘" — 장소 추천 요청이 아님
    }

    // 위치를 명시하지 않고 상대적으로 가리킨 경우 무엇을 기준으로 했는지.
    public enum RelativeLocationKind
    {
        None,
        NearUser,       // "근처", "내 주변", "여기", "현재 위치"
        NearSchedule    // "일정 근처", "숙소 근처"
    }

    // TravelQueryAnalyzer의 분석 결과. LLM은 이 값을 다시 판단하지 않고 그대로 받는다.
    public sealed record TravelQueryAnalysis(
        TravelQueryIntent Intent,
        string? Location,               // 검색 중심으로 쓸 지역/랜드마크 이름(예: "강남", "경복궁"). 없으면 null
        PlaceCategory Category,
        IReadOnlyList<string> Keywords, // 카테고리 판단에 쓴 단어들(예: "한식집", "파스타")
        string? SpecificPlace,          // 특정 가게/지점 이름(예: "스타벅스 강남역점"). 없으면 null
        RelativeLocationKind RelativeLocation,
        string? CuisineKeyword,         // 음식 세부 종류(한식/중식/일식/양식/분식). 검색 조건을 더 좁히는 데 쓴다
        string? UserPreference,         // 분위기·가격 등 선호 표현(예: "조용한 가성비"). 없으면 null
        bool MentionsToday,             // "오늘"이 들어 있는지(오늘 일정 기준 검색에 쓴다)
        string? MenuKeyword = null);    // 업종보다 좁은 메뉴/품목(예: "파스타", "마라탕"). 있으면 키워드 검색으로 후보를 좁힌다
}
