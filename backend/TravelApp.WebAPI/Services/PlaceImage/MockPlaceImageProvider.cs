namespace TravelApp.WebAPI.Services.PlaceImage
{
    // 실제 사진 API 키가 없는 동안 쓰는 기본 provider. 항상 null(사진 없음)을 돌려준다 — 가짜 이미지 URL을 만들면
    // 실제 장소 사진처럼 보일 수 있어서다. 클라이언트는 null이면 자체 사진 조회 또는 "사진 없음" 플레이스홀더로 처리한다.
    public class MockPlaceImageProvider : IPlaceImageProvider
    {
        public Task<string?> GetRepresentativeImageUrlAsync(
            string placeName,
            double? latitude,
            double? longitude,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
