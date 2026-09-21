namespace TravelApp.WebAPI.Services.RouteOptimization
{
    // 여러 지점 간 실제 이동시간(초) 행렬을 구해오는 provider.
    // 구현체를 교체해도(Haversine -> Tmap 등) 상위 알고리즘(Held-Karp/OR-Tools/GA)은 그대로 재사용된다.
    public interface ITravelTimeProvider
    {
        // points[i] -> points[j] 이동시간(초)을 담은 N x N 행렬. i == j는 0.
        // 도로가 일방통행일 수 있어 대칭이 아닐 수 있으므로 (i, j)와 (j, i)를 모두 채운다.
        Task<double[,]> GetTravelTimeMatrixAsync(
            IReadOnlyList<GeoPoint> points,
            DateTime departureTime,
            CancellationToken cancellationToken = default);
    }
}
