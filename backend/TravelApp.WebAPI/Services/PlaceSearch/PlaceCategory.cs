namespace TravelApp.WebAPI.Services.PlaceSearch
{
    // 사용자가 찾는 장소 종류. 검색 API의 카테고리 조건으로 쓰인다 — LLM에게 후보 전체를 주고
    // 알아서 걸러내게 하지 않고, 검색 단계에서 먼저 좁힌다.
    public enum PlaceCategory
    {
        Other,
        Restaurant,
        Cafe,
        Bar,
        Attraction,
        Shopping,
        Hotel
    }

    public static class PlaceCategoryText
    {
        // 사용자에게 보이는 문구용 한국어 이름.
        public static string ToKorean(PlaceCategory category) => category switch
        {
            PlaceCategory.Restaurant => "음식점",
            PlaceCategory.Cafe => "카페",
            PlaceCategory.Bar => "술집",
            PlaceCategory.Attraction => "관광지",
            PlaceCategory.Shopping => "쇼핑 장소",
            PlaceCategory.Hotel => "숙소",
            _ => "장소"
        };
    }
}
