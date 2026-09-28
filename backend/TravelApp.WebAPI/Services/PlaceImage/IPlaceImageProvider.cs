namespace TravelApp.WebAPI.Services.PlaceImage
{
    // 챗봇 추천 카드에 붙일 장소 대표 사진 URL을 가져오는 provider. 사진은 LLM이 아니라 이 provider가 정한다
    // (LLM이 존재하지 않는 이미지 URL을 지어내지 않게). 구현체는 설정("PlaceImage:Provider")으로 교체한다 —
    // 지금은 Mock뿐이고, 실제 API(카카오 로컬, 구글 플레이스 포토, 네이버 검색 등)는 새 구현체를 추가해 붙인다.
    public interface IPlaceImageProvider
    {
        // 사진을 찾지 못하면 null. 클라이언트는 null이면 "사진 없음" 플레이스홀더를 보여준다.
        // 좌표는 같은 이름의 다른 지점 사진을 고르지 않게 하는 데 쓴다(없을 수 있음).
        Task<string?> GetRepresentativeImageUrlAsync(
            string placeName,
            double? latitude,
            double? longitude,
            CancellationToken cancellationToken = default);
    }
}
