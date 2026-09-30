using System.Text;
using System.Text.Json;
using SharedData.DTOs;

namespace TravelApp.WebAPI.Services.PlaceStatus
{
    // Google Places API(New) Text Search(POST /v1/places:searchText)로 businessStatus를 확인해
    // Tmap POI 응답에 없는 영업상태(휴관/폐업)를 보완한다. 문서: https://developers.google.com/maps/documentation/places/web-service/text-search
    // 이 provider는 보조 신호일 뿐 추천 그라운딩의 필수 경로가 아니므로, HTTP 실패나 네트워크 예외가
    // 나도 예외를 던지지 않고 로그만 남기고 null을 돌려준다(호출한 쪽이 기존 상태를 그대로 유지).
    public class GooglePlacesOperatingStatusProvider : IPlaceOperatingStatusProvider
    {
        private const string SearchTextEndpoint = "https://places.googleapis.com/v1/places:searchText";

        // 동명이인/다른 지점과 헷갈리지 않도록 좌표 바이어스를 좁게 준다. locationBias는 결과를 보장하지
        // 않고 힌트일 뿐이라 textQuery(장소 이름)와 함께 써야 한다.
        private const double LocationBiasRadiusMeters = 200;

        private readonly HttpClient _httpClient;
        private readonly ILogger<GooglePlacesOperatingStatusProvider> _logger;
        private readonly string _apiKey;

        public GooglePlacesOperatingStatusProvider(
            HttpClient httpClient, IConfiguration configuration, ILogger<GooglePlacesOperatingStatusProvider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _apiKey = configuration["Google:PlacesApiKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_apiKey) ||
                _apiKey.Contains(SecretsConfigurationCheck.PlaceholderMarker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Google:PlacesApiKey가 설정되지 않았습니다. Google Cloud Console에서 발급받은 Places API(New) 키를 다음 명령으로 설정하세요:\n" +
                    "  dotnet user-secrets set \"Google:PlacesApiKey\" \"<YOUR_API_KEY>\" --project backend/TravelApp.WebAPI\n" +
                    "(운영에서는 환경변수 Google__PlacesApiKey)");
            }
        }

        public async Task<PlaceOperatingStatusResult?> GetOperatingStatusAsync(
            string placeName, double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            try
            {
                var requestBody = new
                {
                    textQuery = placeName,
                    languageCode = "ko",
                    locationBias = new
                    {
                        circle = new
                        {
                            center = new { latitude, longitude },
                            radius = LocationBiasRadiusMeters
                        }
                    }
                };

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SearchTextEndpoint)
                {
                    Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Add("X-Goog-Api-Key", _apiKey);
                httpRequest.Headers.Add("X-Goog-FieldMask", "places.businessStatus,places.displayName");

                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                string body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Google Places 영업상태 조회가 실패했습니다 ({StatusCode}): {Body}", (int)response.StatusCode, Truncate(body));
                    return null;
                }

                return ParseBusinessStatus(body, placeName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Google Places 영업상태 조회 중 오류가 발생했습니다 (place={PlaceName})", placeName);
                return null;
            }
        }

        // Text Search는 locationBias가 있으면 질의어와 이름이 거의 안 맞아도 근처의 그럴듯한 장소를 반환한다
        // (실측 확인: "가상현실맛집XYZ" 질의에 무관한 "Starfield Coex Mall"이 매칭됨). places[0]의 businessStatus를
        // 그대로 믿으면 존재하지 않거나 실제 폐업한 장소도 근처 다른 영업중인 곳 때문에 Open으로 잘못 보완될
        // 수 있어서, 매칭된 displayName이 질의한 이름과 충분히 비슷한지(PlaceRecommendationGrounder와 동일한
        // 기준) 먼저 확인하고, 미달이면 businessStatus를 신뢰하지 않고 null(Unknown 유지)을 돌려준다.
        private PlaceOperatingStatusResult? ParseBusinessStatus(string body, string placeName)
        {
            using var json = JsonDocument.Parse(body);

            if (!json.RootElement.TryGetProperty("places", out var places) ||
                places.ValueKind != JsonValueKind.Array ||
                places.GetArrayLength() == 0)
            {
                return null;
            }

            var first = places[0];

            string matchedName = first.TryGetProperty("displayName", out var displayName) &&
                displayName.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
                ? text.GetString() ?? string.Empty
                : string.Empty;

            if (!PlaceRecommendationGrounder.IsSufficientlySimilar(placeName, matchedName))
            {
                _logger.LogInformation(
                    "Google Places 매칭된 장소 이름이 질의와 달라 businessStatus를 신뢰하지 않습니다 (질의={PlaceName}, 매칭={MatchedName})",
                    placeName, matchedName);
                return null;
            }

            if (!first.TryGetProperty("businessStatus", out var statusElement) ||
                statusElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            return statusElement.GetString() switch
            {
                "OPERATIONAL" => new PlaceOperatingStatusResult(PlaceOperatingStatus.Open, false, now),
                "CLOSED_TEMPORARILY" => new PlaceOperatingStatusResult(PlaceOperatingStatus.Closed, false, now),
                "CLOSED_PERMANENTLY" => new PlaceOperatingStatusResult(PlaceOperatingStatus.Closed, true, now),
                _ => null
            };
        }

        private static string Truncate(string body) => body.Length <= 300 ? body : body[..300] + "...";
    }
}
