namespace TravelApp.WebAPI.Services.PlaceSearch
{
    // 주변 카테고리 검색 요청 하나. Keyword는 "카페", "맛집" 등 자유 검색어이며 null이면 전체 카테고리.
    public readonly record struct NearbyPlaceSearchRequest(
        double Latitude,
        double Longitude,
        int RadiusMeters,
        string? Keyword);
}
