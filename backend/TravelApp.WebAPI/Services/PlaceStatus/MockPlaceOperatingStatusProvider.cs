namespace TravelApp.WebAPI.Services.PlaceStatus
{
    // 기본 provider: 항상 null(확인 불가)을 돌려줘 영업상태 보완 기능을 켜기 전과 동일하게 동작한다
    // (Google Places API 키가 준비되기 전까지는 이 provider가 기본값이다).
    public class MockPlaceOperatingStatusProvider : IPlaceOperatingStatusProvider
    {
        public Task<PlaceOperatingStatusResult?> GetOperatingStatusAsync(
            string placeName,
            double latitude,
            double longitude,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PlaceOperatingStatusResult?>(null);
        }
    }
}
