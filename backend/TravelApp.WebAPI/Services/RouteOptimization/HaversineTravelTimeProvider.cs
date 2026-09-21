namespace TravelApp.WebAPI.Services.RouteOptimization
{
    // 임시 provider: 직선거리(Haversine) / 평균 주행 속도로 이동시간을 추정한다.
    // 실제 도로망·정체를 반영하지 않으므로 Tmap(SK Open API) 사용 권한이 승인되면
    // TmapTravelTimeProvider로 교체해야 한다 (DI 등록만 바꾸면 되도록 ITravelTimeProvider로 분리해둠).
    public class HaversineTravelTimeProvider : ITravelTimeProvider
    {
        private const double EarthRadiusMeters = 6_371_000;

        // 도심 자동차 평균 주행 속도 가정치 (km/h). 직선거리를 도로 주행거리로 보정하기 위해
        // 실제 도로거리는 직선거리의 약 1.3배라고 가정하고 나눗셈에 반영한다.
        private const double AssumedAverageSpeedKmh = 25.0;
        private const double StraightLineToRoadDistanceFactor = 1.3;

        public Task<double[,]> GetTravelTimeMatrixAsync(
            IReadOnlyList<GeoPoint> points,
            DateTime departureTime,
            CancellationToken cancellationToken = default)
        {
            int n = points.Count;
            var matrix = new double[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    double distanceMeters = HaversineDistanceMeters(
                        points[i].Latitude, points[i].Longitude,
                        points[j].Latitude, points[j].Longitude);

                    double roadDistanceMeters = distanceMeters * StraightLineToRoadDistanceFactor;
                    double speedMetersPerSecond = AssumedAverageSpeedKmh * 1000.0 / 3600.0;

                    matrix[i, j] = roadDistanceMeters / speedMetersPerSecond;
                }
            }

            return Task.FromResult(matrix);
        }

        private static double HaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusMeters * c;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
