using SharedData.DTOs;
using TravelApp.WebAPI.Services.PlaceSearch;

namespace TravelApp.WebAPI.Tests
{
    // 이름 검색 결과를 검색어별로 관련성 순서 그대로 지정해 두는 가짜 provider. 지정하지 않은 검색어는 빈 목록(못 찾음).
    // 실제 Tmap 응답 순서를 재현해, 1순위가 엉뚱한 곳일 때도 뒤 순위를 제대로 훑는지 검증하는 데 쓴다.
    // 주변 검색은 NearbyResults를 그대로 돌려주고, 받은 요청을 NearbyRequests에 남긴다(검색 중심/카테고리 검증용).
    internal sealed class FakeNameSearchProvider(Dictionary<string, PlaceSearchResultDto[]> byQuery) : INearbyPlaceSearchProvider
    {
        public List<string> Queries { get; } = new();

        public List<NearbyPlaceSearchRequest> NearbyRequests { get; } = new();

        public List<PlaceSearchResultDto> NearbyResults { get; set; } = new();

        // 설정하면 모든 검색 호출이 이 예외를 던진다(검색 API 장애 재현).
        public Exception? ThrowOnSearch { get; set; }

        public Task<List<PlaceSearchResultDto>> SearchNearbyAsync(NearbyPlaceSearchRequest request, CancellationToken cancellationToken = default)
        {
            NearbyRequests.Add(request);

            if (ThrowOnSearch != null)
            {
                throw ThrowOnSearch;
            }

            // 호출마다 새 객체를 돌려준다(검색 서비스가 거리/표시 이름을 채워 넣으므로 테스트 데이터를 오염시키지 않게).
            return Task.FromResult(NearbyResults.Select(Clone).ToList());
        }

        public Task<IReadOnlyList<PlaceSearchResultDto>> SearchByNameAsync(string placeName, CancellationToken cancellationToken = default)
        {
            Queries.Add(placeName);

            if (ThrowOnSearch != null)
            {
                throw ThrowOnSearch;
            }

            return Task.FromResult<IReadOnlyList<PlaceSearchResultDto>>(
                (byQuery.GetValueOrDefault(placeName) ?? []).Select(Clone).ToList());
        }

        public static PlaceSearchResultDto Place(
            string name, bool closed = false, DateTime? lastConfirmed = null,
            double latitude = 37.57, double longitude = 126.98, string category = "", string address = "") => new()
        {
            PlaceId = $"id:{name}",
            Name = name,
            Category = category,
            Address = address,
            Latitude = latitude,
            Longitude = longitude,
            IsPermanentlyClosed = closed,
            LastConfirmedOperatingDate = lastConfirmed
        };

        private static PlaceSearchResultDto Clone(PlaceSearchResultDto p) => new()
        {
            PlaceId = p.PlaceId,
            Name = p.Name,
            CanonicalName = p.CanonicalName,
            Category = p.Category,
            Address = p.Address,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            DistanceMeters = p.DistanceMeters,
            OperatingStatus = p.OperatingStatus,
            Source = p.Source,
            SearchConfidence = p.SearchConfidence,
            IsPermanentlyClosed = p.IsPermanentlyClosed,
            LastConfirmedOperatingDate = p.LastConfirmedOperatingDate
        };
    }
}
