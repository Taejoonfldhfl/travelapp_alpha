using SharedData.DTOs;
using TravelApp.WebAPI.Services.RouteOptimization;

namespace TravelApp.WebAPI.Services.PlaceSearch
{
    // 사용자가 말한 지역/랜드마크/가게 이름을 검색 중심 좌표로 바꾼다. 기존 이름 검색(SearchByNameAsync)을 재사용하되,
    // 1순위 결과를 그대로 쓰지 않고 상위 결과를 점수로 비교한다. 확정하지 못하면 null — 그 경우 호출한 쪽은
    // 추천하지 않고 이유를 알려야 하며, LLM에게 좌표를 만들게 해서는 안 된다.
    public class LocationResolver
    {
        // 흔히 쓰는 줄임말은 이름 검색으로 찾히지 않거나(예: '홍대' -> 홍익대학교 캠퍼스), 구 전체 중심(서울 강남구 청담동)처럼
        // 사람들이 뜻하는 곳과 다른 좌표가 나온다. 이런 이름만 사람들이 실제로 뜻하는 기준점으로 바꿔 검색한다.
        private static readonly Dictionary<string, string> AreaAliases = new(StringComparer.Ordinal)
        {
            ["강남"] = "강남역",
            ["홍대"] = "홍대입구역",
            ["건대"] = "건대입구역",
            ["서울대"] = "서울대입구역"
        };

        // "X에 있는 Y"의 Y를 찾을 때, 결과가 X에서 이 거리 안에 있어야 같은 곳으로 본다(다른 지역 동명 가게 배제).
        private const double RegionHintMaxDistanceMeters = 3000;

        // 점수: 이름 일치가 가장 중요하고, 그다음 지역/주소 일치, 장소 타입, 검색 순위, 기준점과의 거리 순이다.
        private const double ExactNameScore = 100;
        private const double AreaNameScore = 80;
        private const double SimilarNameScore = 60;
        private const double AddressOnlyScore = 30;
        private const double AreaOrLandmarkTypeBonus = 10;
        private const double FacilityPenalty = 60;
        private const double RankBonusPerStep = 2;
        private const double MaxDistanceBonus = 15;
        private const double DistanceBonusScaleKm = 50;

        private readonly INearbyPlaceSearchProvider _placeSearchProvider;

        public LocationResolver(INearbyPlaceSearchProvider placeSearchProvider)
        {
            _placeSearchProvider = placeSearchProvider;
        }

        // 지역/랜드마크(예: "강남", "경복궁", "해운대")를 좌표로. referencePoint(여행 지역 중심이나 GPS)는
        // 같은 이름의 지역이 여러 곳일 때(예: 서울 강남구 vs 진주시 강남동) 가까운 쪽을 고르는 데만 쓴다.
        public async Task<ResolvedLocation?> ResolveAreaAsync(
            string locationName,
            (double Latitude, double Longitude)? referencePoint,
            CancellationToken cancellationToken = default)
        {
            string query = AreaAliases.GetValueOrDefault(locationName.Trim(), locationName.Trim());
            var results = await _placeSearchProvider.SearchByNameAsync(query, cancellationToken);

            ResolvedLocation? best = null;
            double bestScore = double.MinValue;

            for (int rank = 0; rank < results.Count; rank++)
            {
                var place = results[rank];
                double? score = ScoreAreaCandidate(place, query, locationName, rank, referencePoint);

                if (score is double s && s > bestScore)
                {
                    bestScore = s;
                    best = new ResolvedLocation(place.CanonicalName.Length > 0 ? place.CanonicalName : place.Name,
                        place.Latitude, place.Longitude, place.Address, Math.Clamp(s / ExactNameScore, 0, 1), place);
                }
            }

            // 부속 시설(주차장 등)만 남았다면 기준점으로 쓰지 않는다.
            return best != null && bestScore > 0 ? best : null;
        }

        // 특정 가게/지점(예: "스타벅스 강남역점", "몽로")을 좌표로. 이름이 같거나 지점만 다른 결과만 인정하며(다른 지점으로
        // 바꿔치기하지 않음), regionHint("광화문에 있는 몽로"의 광화문)가 있으면 그 근처 결과만 인정한다.
        public async Task<ResolvedLocation?> ResolveSpecificPlaceAsync(
            string placeName,
            string? regionHint,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default)
        {
            ResolvedLocation? region = null;

            if (!string.IsNullOrWhiteSpace(regionHint))
            {
                region = await ResolveAreaAsync(regionHint, null, cancellationToken);
            }

            var results = await _placeSearchProvider.SearchByNameAsync(placeName, cancellationToken);

            var acceptable = results
                .Select((place, rank) => (place, rank))
                .Where(x => !PlaceNames.IsFacility(x.place.Name) &&
                            PlaceRecommendationGrounder.IsSufficientlySimilar(placeName, x.place.Name) &&
                            PlaceRecommendationGrounder.IsOperatingAsOf(x.place, asOfUtc))
                .Where(x => region == null || DistanceMeters(region.Latitude, region.Longitude, x.place) <= RegionHintMaxDistanceMeters)
                // 이름이 정확히 같은 결과를 지점만 다른 결과보다 먼저, 같은 조건이면 검색 순위대로.
                .OrderByDescending(x => PlaceRecommendationGrounder.IsExactName(placeName, x.place.Name))
                .ThenBy(x => x.rank)
                .ToList();

            if (acceptable.Count == 0)
            {
                return null;
            }

            var (match, matchRank) = acceptable[0];
            return new ResolvedLocation(match.CanonicalName.Length > 0 ? match.CanonicalName : match.Name,
                match.Latitude, match.Longitude, match.Address, PlaceNames.RankConfidence(matchRank), match);
        }

        private static double? ScoreAreaCandidate(
            PlaceSearchResultDto place, string query, string originalName, int rank, (double Latitude, double Longitude)? referencePoint)
        {
            string name = Normalize(PlaceNames.StripDecorations(place.Name));
            string normalizedQuery = Normalize(query);
            string normalizedOriginal = Normalize(originalName);

            bool exact = name == normalizedQuery || name == normalizedOriginal;
            bool isArea = IsAreaOrLandmark(place);
            bool areaName = isArea && (name.Contains(normalizedQuery, StringComparison.Ordinal) || name.Contains(normalizedOriginal, StringComparison.Ordinal));
            bool similar = PlaceRecommendationGrounder.IsSufficientlySimilar(query, place.Name);
            bool addressMatch = Normalize(place.Address).Contains(normalizedOriginal, StringComparison.Ordinal);

            if (!exact && !areaName && !similar && !addressMatch)
            {
                return null;
            }

            double score = exact ? ExactNameScore : areaName ? AreaNameScore : similar ? SimilarNameScore : AddressOnlyScore;

            if (isArea)
            {
                score += AreaOrLandmarkTypeBonus;
            }

            if (PlaceNames.IsFacility(place.Name))
            {
                score -= FacilityPenalty;
            }

            score += (INearbyPlaceSearchProvider.MaxNameSearchResults - rank) * RankBonusPerStep;

            if (referencePoint is { } reference)
            {
                double distanceKm = DistanceMeters(reference.Latitude, reference.Longitude, place) / 1000.0;
                score += MaxDistanceBonus * Math.Exp(-distanceKm / DistanceBonusScaleKm);
            }

            return score;
        }

        // Tmap 업종 분류가 지역(AOI)이거나 관광명소/지하철역이면 검색 중심으로 쓰기 좋은 장소다.
        private static bool IsAreaOrLandmark(PlaceSearchResultDto place) =>
            place.Category.StartsWith("AOI", StringComparison.Ordinal) ||
            place.Category.Contains("관광명소", StringComparison.Ordinal) ||
            place.Category.Contains("지하철", StringComparison.Ordinal);

        private static double DistanceMeters(double latitude, double longitude, PlaceSearchResultDto place) =>
            HaversineTravelTimeProvider.HaversineDistanceMeters(latitude, longitude, place.Latitude, place.Longitude);

        private static string Normalize(string value) => value.Replace(" ", string.Empty).Trim().ToLowerInvariant();
    }

    // 검색 중심으로 확정된 위치. Place는 이름 검색에서 고른 원래 결과다.
    public sealed record ResolvedLocation(
        string Label,
        double Latitude,
        double Longitude,
        string Address,
        double Confidence,
        PlaceSearchResultDto Place);
}
