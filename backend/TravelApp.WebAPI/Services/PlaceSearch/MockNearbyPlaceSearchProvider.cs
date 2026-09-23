using System;
using SharedData.DTOs;

namespace TravelApp.WebAPI.Services.PlaceSearch
{
    // 임시 provider: 실제 Tmap(SK Open API) "주변 카테고리 검색" 없이, 요청 중심점 주변에
    // 고정된 템플릿 장소들을 방위각/거리로 배치해 "좌표가 확정된 실제 장소 목록"처럼 돌려준다.
    // 반환값은 항상 정확한 위도/경도를 가지므로, 이 provider의 결과만 추천 후보로 쓰면
    // 규칙 2(좌표 없는 답변 차단)가 항상 만족된다.
    // 실제 Tmap 연동이 준비되면 TmapNearbyPlaceSearchProvider로 교체한다 (DI 등록만 바꾸면 됨).
    public class MockNearbyPlaceSearchProvider : INearbyPlaceSearchProvider
    {
        private const double EarthRadiusMeters = 6_371_000;
        private const string SourceName = "Mock";

        // SearchByNameAsync는 중심 좌표를 받지 않으므로(사용자 위치와 무관하게 동작해야 해서),
        // mock 좌표를 만들 때 쓸 임시 기준점(서울시청)이다. 실제 Tmap 이름 검색으로 교체되면 불필요해진다.
        private const double DefaultReferenceLatitude = 37.5663;
        private const double DefaultReferenceLongitude = 126.9779;

        // (이름, 카테고리, 중심점 기준 방위각(도), 중심점 기준 거리(m))
        private static readonly (string Name, string Category, double BearingDeg, double DistanceMeters)[] Templates =
        [
            ("계절밥상 본점", "음식점", 10, 350),
            ("스타벅스 리저브점", "카페", 80, 220),
            ("골목집 김치찌개", "음식점", 150, 480),
            ("블루보틀 로스터리", "카페", 205, 600),
            ("동네책방 서가", "기타", 265, 150),
            ("전망대 카페", "카페", 320, 900),
            ("이자카야 하루", "주점", 35, 700),
            ("올리브영", "편의점", 190, 300),
        ];

        // 메뉴 키워드 검색(SearchText)에서만 나오는 장소들. (이름, 카테고리, 메뉴, 방위각, 거리)
        // 업종 검색(음식점 전체)에서는 나오지 않고, '파스타'처럼 메뉴로 찾을 때만 나와서 키워드 반영 여부를 확인할 수 있다.
        private static readonly (string Name, string Category, string Menu, double BearingDeg, double DistanceMeters)[] MenuTemplates =
        [
            ("파스타 하우스", "음식점", "파스타", 45, 300),
            ("트라토리아 로마", "음식점", "파스타", 120, 650),
            ("화덕피자 공방", "음식점", "피자", 250, 400),
        ];

        // 이름 검색에서만 나오는, 폐업했거나 정보가 오래된 장소들. LLM이 학습 시점 지식으로
        // 이미 문 닫은 곳을 추천하는 상황을 mock으로 재현하기 위한 데이터다. SearchNearbyAsync
        // (주변 카테고리 검색) 결과에는 일부러 포함하지 않는다 — 실제 주변 검색 API도 폐업한
        // 곳은 목록에 잘 노출하지 않기 때문이다. (이름, 카테고리, 폐업 여부, 마지막 영업 확인 후 경과 개월 수)
        private static readonly (string Name, string Category, bool IsPermanentlyClosed, int MonthsSinceLastConfirmedOperating)[] ClosedOrStaleEntries =
        [
            ("추억의 분식집", "음식점", true, 8),
            ("오래된 카페", "카페", false, 5),
        ];

        // 이름만으로 실제 존재 여부/좌표/영업 여부를 확인한다(중심 좌표 없이, 사용자의 현재
        // 위치와 무관). Mock에서는 Templates 안에 있는 이름만 "실존하며 영업 중"으로,
        // ClosedOrStaleEntries에 있는 이름은 "실존하지만 폐업했거나 정보가 오래됨"으로 간주한다
        // (실제 서비스 기준점이 없으므로 서울시청을 임시 기준점으로 삼아 위치를 만든다).
        // 둘 다에 없는 이름(=LLM이 지어낸 장소)은 빈 목록을 반환해 규칙 2가 걸러내도록 한다.
        // 결과 순서는 Templates, ClosedOrStaleEntries에 정의된 순서이고 최대 MaxNameSearchResults건이다.
        public Task<IReadOnlyList<PlaceSearchResultDto>> SearchByNameAsync(
            string placeName,
            CancellationToken cancellationToken = default)
        {
            string normalizedQuery = Normalize(placeName);
            var results = new List<PlaceSearchResultDto>();

            foreach (var template in Templates)
            {
                if (!NameMatches(template.Name, normalizedQuery))
                {
                    continue;
                }

                var (lat, lng) = DestinationPoint(
                    DefaultReferenceLatitude, DefaultReferenceLongitude,
                    template.BearingDeg, template.DistanceMeters);

                results.Add(new PlaceSearchResultDto
                {
                    PlaceId = $"mock:{template.Name}:{lat:F5},{lng:F5}",
                    Name = template.Name,
                    CanonicalName = template.Name,
                    Category = template.Category,
                    Address = "이름 검색으로 확인된 장소 (mock 데이터)",
                    Latitude = lat,
                    Longitude = lng,
                    Source = SourceName,
                    SearchConfidence = PlaceNames.RankConfidence(results.Count)
                });
            }

            foreach (var entry in ClosedOrStaleEntries)
            {
                if (!NameMatches(entry.Name, normalizedQuery))
                {
                    continue;
                }

                // 폐업/오래된 정보 데이터에는 좌표 자체는 있는 것으로 채워준다 — 판단 기준은
                // 좌표 유무가 아니라 IsPermanentlyClosed/LastConfirmedOperatingDate이기 때문이다.
                var (lat, lng) = DestinationPoint(DefaultReferenceLatitude, DefaultReferenceLongitude, 0, 100);

                results.Add(new PlaceSearchResultDto
                {
                    PlaceId = $"mock:{entry.Name}:{lat:F5},{lng:F5}",
                    Name = entry.Name,
                    CanonicalName = entry.Name,
                    Category = entry.Category,
                    Address = "이름 검색으로 확인된 장소 (mock 데이터, 폐업/오래된 정보)",
                    Latitude = lat,
                    Longitude = lng,
                    Source = SourceName,
                    SearchConfidence = PlaceNames.RankConfidence(results.Count),
                    // 폐업은 Closed, 정보만 오래된 곳은 지금 영업 여부를 모르므로 Unknown.
                    OperatingStatus = entry.IsPermanentlyClosed ? PlaceOperatingStatus.Closed : PlaceOperatingStatus.Unknown,
                    IsPermanentlyClosed = entry.IsPermanentlyClosed,
                    LastConfirmedOperatingDate = DateTime.UtcNow.AddMonths(-entry.MonthsSinceLastConfirmedOperating)
                });
            }

            // 어디에도 없는 이름 = mock 세계에는 존재하지 않는 장소 -> 빈 목록(못 찾음).
            return Task.FromResult<IReadOnlyList<PlaceSearchResultDto>>(
                results.Take(INearbyPlaceSearchProvider.MaxNameSearchResults).ToList());
        }

        private static bool NameMatches(string candidateName, string normalizedQuery)
        {
            string normalizedCandidate = Normalize(candidateName);

            return normalizedCandidate == normalizedQuery ||
                (normalizedCandidate.Length >= 2 && normalizedQuery.Contains(normalizedCandidate)) ||
                (normalizedQuery.Length >= 2 && normalizedCandidate.Contains(normalizedQuery));
        }

        private static string Normalize(string value) => value.Replace(" ", "").Trim().ToLowerInvariant();

        public Task<List<PlaceSearchResultDto>> SearchNearbyAsync(
            NearbyPlaceSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            var results = new List<PlaceSearchResultDto>();

            // 메뉴 키워드 검색: 업종 필터 없이, 메뉴가 같거나 이름에 검색어가 들어간 장소만.
            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                var matches = MenuTemplates
                    .Where(t => t.Menu == request.SearchText || t.Name.Contains(request.SearchText, StringComparison.Ordinal))
                    .Select(t => (t.Name, t.Category, t.BearingDeg, t.DistanceMeters))
                    .Concat(Templates
                        .Where(t => t.Name.Contains(request.SearchText, StringComparison.Ordinal))
                        .Select(t => (t.Name, t.Category, t.BearingDeg, t.DistanceMeters)));

                foreach (var (name, category, bearing, distance) in matches)
                {
                    if (distance <= request.RadiusMeters)
                    {
                        results.Add(ToNearbyResult(request, name, category, bearing, distance));
                    }
                }

                return Task.FromResult(request.MaxResults is int limit ? results.Take(limit).ToList() : results);
            }

            foreach (var template in Templates)
            {
                if (template.DistanceMeters > request.RadiusMeters)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(request.Keyword) &&
                    !MatchesKeyword(template.Name, template.Category, request.Keyword))
                {
                    continue;
                }

                if (!MatchesCategory(template.Category, request.Category))
                {
                    continue;
                }

                results.Add(ToNearbyResult(request, template.Name, template.Category, template.BearingDeg, template.DistanceMeters));
            }

            var limited = request.MaxResults is int max ? results.Take(max).ToList() : results;
            return Task.FromResult(limited);
        }

        private static PlaceSearchResultDto ToNearbyResult(
            NearbyPlaceSearchRequest request, string name, string category, double bearingDeg, double distanceMeters)
        {
            var (lat, lng) = DestinationPoint(request.Latitude, request.Longitude, bearingDeg, distanceMeters);

            return new PlaceSearchResultDto
            {
                PlaceId = $"mock:{name}:{lat:F5},{lng:F5}",
                Name = name,
                CanonicalName = name,
                Category = category,
                Address = $"중심점에서 약 {distanceMeters:F0}m 지점 (mock 데이터)",
                Latitude = lat,
                Longitude = lng,
                DistanceMeters = distanceMeters,
                Source = SourceName
            };
        }

        private static bool MatchesCategory(string templateCategory, PlaceCategory category) => category switch
        {
            PlaceCategory.Other => true,
            PlaceCategory.Restaurant => templateCategory == "음식점",
            PlaceCategory.Cafe => templateCategory == "카페",
            PlaceCategory.Bar => templateCategory == "주점",
            PlaceCategory.Attraction or PlaceCategory.Shopping => templateCategory == "기타",
            _ => false
        };

        private static bool MatchesKeyword(string name, string category, string keyword)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                category.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 흔히 쓰는 표현을 카테고리로 느슨하게 매핑한다.
            return keyword switch
            {
                "맛집" or "식당" or "음식점" or "밥집" => category == "음식점",
                "카페" or "커피" => category == "카페",
                "술집" or "바" or "이자카야" => category == "주점",
                _ => false
            };
        }

        // 구면 삼각법으로 (bearingDeg, distanceMeters)만큼 떨어진 목적지 좌표를 계산한다.
        private static (double Latitude, double Longitude) DestinationPoint(
            double latitude, double longitude, double bearingDeg, double distanceMeters)
        {
            double angularDistance = distanceMeters / EarthRadiusMeters;
            double bearing = ToRadians(bearingDeg);
            double lat1 = ToRadians(latitude);
            double lon1 = ToRadians(longitude);

            double lat2 = Math.Asin(
                Math.Sin(lat1) * Math.Cos(angularDistance) +
                Math.Cos(lat1) * Math.Sin(angularDistance) * Math.Cos(bearing));

            double lon2 = lon1 + Math.Atan2(
                Math.Sin(bearing) * Math.Sin(angularDistance) * Math.Cos(lat1),
                Math.Cos(angularDistance) - Math.Sin(lat1) * Math.Sin(lat2));

            return (ToDegrees(lat2), ToDegrees(lon2));
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
        private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
    }
}
