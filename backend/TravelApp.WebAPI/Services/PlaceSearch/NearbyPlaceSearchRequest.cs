namespace TravelApp.WebAPI.Services.PlaceSearch
{
    // 주변 카테고리 검색 요청 하나. Keyword는 "한식", "카페" 등 검색 API에 그대로 넘기는 세부 업종이며
    // null이면 Category로 업종을 정한다. Category도 Other면 전체 업종이다.
    // MaxResults가 null이면 provider 기본 개수를 쓴다. (뒤 값들은 기본값이 있어 기존 호출부는 그대로 동작한다.)
    // SearchText는 '파스타'처럼 업종 분류가 아닌 자유 검색어다. 값이 있으면 업종 필터 대신 중심점 주변 키워드 검색을 한다
    // (Tmap 업종 필터는 업종명만 받아서 '파스타'를 넣으면 0건이 된다 — 실측 확인).
    public readonly record struct NearbyPlaceSearchRequest(
        double Latitude,
        double Longitude,
        int RadiusMeters,
        string? Keyword,
        PlaceCategory Category = PlaceCategory.Other,
        int? MaxResults = null,
        string? SearchText = null);
}
