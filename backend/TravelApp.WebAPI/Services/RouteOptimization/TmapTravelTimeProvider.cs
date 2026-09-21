using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace TravelApp.WebAPI.Services.RouteOptimization
{
    // Tmap(SK Open API) "자동차 경로안내"(POST /tmap/routes) 엔드포인트로 실제 도로 기반,
    // 정체 상황(trafficInfo=Y)을 반영한 두 지점 간 이동시간을 구한다.
    //
    // SK Open API에는 여러 출발지/목적지를 한 번에 처리하는 "경로 매트릭스" 상품도 있지만,
    // 이 프로젝트는 아직 그 상품에 대한 사용 권한/문서 접근 권한이 없어 정확한 요청 스펙을
    // 확인하지 못했다. 대신 공개적으로 안정적으로 문서화된 단건 경로 API를 지점 쌍(pair)마다
    // 호출해 행렬을 채운다. N개 지점이면 N*(N-1)번 호출되므로 결과를 캐싱한다.
    // 나중에 매트릭스 API 사용 권한이 나오면 이 클래스만 교체하면 된다 (ITravelTimeProvider 계약은 동일).
    public class TmapTravelTimeProvider : ITravelTimeProvider
    {
        private const string Endpoint = "https://apis.openapi.sk.com/tmap/routes?version=1";
        private const int MaxConcurrentRequests = 5;

        // 같은 트립 안에서 같은 좌표쌍을 또 최적화할 때 재호출하지 않도록 짧게 캐싱한다.
        // 정체 상황은 시간에 따라 바뀌므로 TTL을 길게 잡지 않는다.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly string _appKey;

        public TmapTravelTimeProvider(HttpClient httpClient, IMemoryCache cache, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _cache = cache;

            _appKey = configuration["Tmap:AppKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_appKey))
            {
                throw new InvalidOperationException(
                    "Tmap:AppKey가 설정되지 않았습니다. SK Open API에서 자동차 경로안내 상품 사용 신청이 " +
                    "승인된 뒤, 다음 명령으로 키를 설정하세요:\n" +
                    "  dotnet user-secrets set \"Tmap:AppKey\" \"<YOUR_APP_KEY>\" --project backend/TravelApp.WebAPI");
            }
        }

        public async Task<double[,]> GetTravelTimeMatrixAsync(
            IReadOnlyList<GeoPoint> points,
            DateTime departureTime,
            CancellationToken cancellationToken = default)
        {
            int n = points.Count;
            var matrix = new double[n, n];

            using var throttle = new SemaphoreSlim(MaxConcurrentRequests);
            var tasks = new List<Task>();

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    int fromIndex = i;
                    int toIndex = j;

                    tasks.Add(FillCellAsync(matrix, points[fromIndex], points[toIndex], fromIndex, toIndex, departureTime, throttle, cancellationToken));
                }
            }

            await Task.WhenAll(tasks);

            return matrix;
        }

        private async Task FillCellAsync(
            double[,] matrix,
            GeoPoint from,
            GeoPoint to,
            int fromIndex,
            int toIndex,
            DateTime departureTime,
            SemaphoreSlim throttle,
            CancellationToken cancellationToken)
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                matrix[fromIndex, toIndex] = await GetPairTravelTimeSecondsAsync(from, to, departureTime, cancellationToken);
            }
            finally
            {
                throttle.Release();
            }
        }

        private async Task<double> GetPairTravelTimeSecondsAsync(
            GeoPoint from,
            GeoPoint to,
            DateTime departureTime,
            CancellationToken cancellationToken)
        {
            string cacheKey = BuildCacheKey(from, to, departureTime);

            if (_cache.TryGetValue(cacheKey, out double cachedSeconds))
            {
                return cachedSeconds;
            }

            double seconds = await CallTmapRoutesAsync(from, to, cancellationToken);

            _cache.Set(cacheKey, seconds, CacheTtl);

            return seconds;
        }

        private async Task<double> CallTmapRoutesAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken)
        {
            var form = new Dictionary<string, string>
            {
                ["startX"] = from.Longitude.ToString(CultureInfo.InvariantCulture),
                ["startY"] = from.Latitude.ToString(CultureInfo.InvariantCulture),
                ["endX"] = to.Longitude.ToString(CultureInfo.InvariantCulture),
                ["endY"] = to.Latitude.ToString(CultureInfo.InvariantCulture),
                ["reqCoordType"] = "WGS84GEO",
                ["resCoordType"] = "WGS84GEO",
                ["searchOption"] = "0",
                ["trafficInfo"] = "Y"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new FormUrlEncodedContent(form)
            };
            request.Headers.Add("appKey", _appKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Tmap 경로안내 API 호출이 실패했습니다 ({(int)response.StatusCode} {response.StatusCode}): {body}");
            }

            using var json = JsonDocument.Parse(body);

            var properties = json.RootElement
                .GetProperty("features")[0]
                .GetProperty("properties");

            return properties.GetProperty("totalTime").GetDouble();
        }

        private static string BuildCacheKey(GeoPoint from, GeoPoint to, DateTime departureTime)
        {
            // 정체 상황은 시간에 따라 변하므로 출발 시각을 30분 단위로 버킷화해서 캐시 키에 포함한다.
            var bucket = new DateTime(
                departureTime.Year, departureTime.Month, departureTime.Day,
                departureTime.Hour, departureTime.Minute < 30 ? 0 : 30, 0);

            return string.Create(CultureInfo.InvariantCulture, $"tmap:{from.Latitude:F5},{from.Longitude:F5}->{to.Latitude:F5},{to.Longitude:F5}@{bucket:yyyyMMddHHmm}");
        }
    }
}
