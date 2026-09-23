using System.Text.Json;
using SharedData.DTOs;
using SharedData.Models;
using TravelApp.WebAPI.Services.QueryAnalysis;
using TravelApp.WebAPI.Services.RouteOptimization;

namespace TravelApp.WebAPI.Services.PlaceSearch
{
    // 질문 분석 결과로 검색 기준점을 정하고, 그 주변에서 추천 후보를 모은다. 검색 범위는 여기서만 정한다(LLM은 관여하지 않음).
    //
    // 기준점 우선순위: 사용자가 말한 특정 장소 > 사용자가 말한 지역 > "내 주변/근처"(GPS) > 여행 일정 > 현재 위치(GPS).
    // "강남 근처 맛집"처럼 지역을 말했으면 GPS가 있어도 강남을 기준으로 삼는다.
    public class PlaceCandidateSearchService
    {
        // 기준점 종류별 첫 검색 반경(m). 후보가 모자라면 RelaxedRadiusFactor배(최대 MaxRelaxedRadiusMeters)로 한 번만 넓힌다.
        // Tmap은 km 단위 정수 반경만 받으므로 1km 단위로 둔다.
        private const int CurrentLocationRadiusMeters = 1000;
        private const int NamedLocationRadiusMeters = 2000;
        private const int SpecificPlaceRadiusMeters = 1000;
        private const int ScheduleAreaRadiusMeters = 2000;
        private const int TripAreaRadiusMeters = 15000;
        private const int RelaxedRadiusFactor = 2;
        private const int MaxRelaxedRadiusMeters = 5000;

        // 검색 API에 요청하는 개수와, 정리 후 LLM에 넘길 최대 개수. 부속 시설/중복을 걸러내고도 20개 이상 남도록 여유 있게 받는다.
        private const int RequestedCandidateCount = 40;
        private const int MaxCandidatesForPrompt = 30;
        private const int MinCandidatesBeforeRelaxing = 5;

        // 일정의 "오늘"을 판단할 때 쓰는 시간대(앱 사용 지역 기준, 한국 표준시). 일정 시각은 UTC로 저장된다.
        private static readonly TimeSpan LocalUtcOffset = TimeSpan.FromHours(9);

        private readonly INearbyPlaceSearchProvider _placeSearchProvider;
        private readonly LocationResolver _locationResolver;

        public PlaceCandidateSearchService(INearbyPlaceSearchProvider placeSearchProvider, LocationResolver locationResolver)
        {
            _placeSearchProvider = placeSearchProvider;
            _locationResolver = locationResolver;
        }

        public async Task<CandidateSearchResult> SearchAsync(
            TravelQueryAnalysis analysis,
            double? currentLatitude,
            double? currentLongitude,
            IReadOnlyList<Schedule> schedules,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default)
        {
            (double Latitude, double Longitude)? gps =
                currentLatitude.HasValue && currentLongitude.HasValue ? (currentLatitude.Value, currentLongitude.Value) : null;

            try
            {
                var anchorResult = await ResolveAnchorAsync(analysis, gps, schedules, asOfUtc, cancellationToken);

                if (anchorResult.Failure != null)
                {
                    return anchorResult.Failure;
                }

                var anchor = anchorResult.Anchor!;
                var (candidates, radius) = await SearchAroundAsync(anchor, anchorResult.RadiusMeters, analysis, cancellationToken);

                // 이미 일정에 있는 장소는 LLM에 후보로 주지 않는다(같은 곳 재추천을 프롬프트가 아니라 서버에서 막는다).
                int beforeExclusion = candidates.Count;
                candidates = ExcludeScheduledPlaces(candidates, schedules).ToList();
                int excludedAsScheduled = beforeExclusion - candidates.Count;

                // 특정 장소를 물었으면 그 장소 자체가 항상 첫 후보다(주변 검색에 안 잡혀도).
                if (anchorResult.AnchorPlace is { } anchorPlace)
                {
                    anchorPlace.DistanceMeters = 0;
                    anchorPlace.CanonicalName = PlaceNames.ToCanonicalName(anchorPlace.Name);
                    candidates = Deduplicate(new[] { anchorPlace }.Concat(candidates)).ToList();
                }

                if (candidates.Count == 0)
                {
                    return new CandidateSearchResult(ChatSearchStatus.NoCandidates, anchor, radius, [],
                        NoCandidatesMessage(analysis, anchor), ExcludedAsScheduled: excludedAsScheduled);
                }

                return new CandidateSearchResult(ChatSearchStatus.Success, anchor, radius, candidates, null,
                    ExcludedAsScheduled: excludedAsScheduled);
            }
            catch (Exception ex) when (IsSearchServiceFailure(ex, cancellationToken))
            {
                return new CandidateSearchResult(ChatSearchStatus.SearchUnavailable, null, 0, [],
                    "장소 검색 서비스를 일시적으로 사용할 수 없어요. 잠시 후 다시 시도해 주세요.", ex.Message);
            }
        }

        private async Task<AnchorResolution> ResolveAnchorAsync(
            TravelQueryAnalysis analysis,
            (double Latitude, double Longitude)? gps,
            IReadOnlyList<Schedule> schedules,
            DateTime asOfUtc,
            CancellationToken cancellationToken)
        {
            var tripCenter = Centroid(schedules);

            switch (analysis.Intent)
            {
                case TravelQueryIntent.SpecificPlace:
                {
                    var place = await _locationResolver.ResolveSpecificPlaceAsync(
                        analysis.SpecificPlace!, analysis.Location, asOfUtc, cancellationToken);

                    if (place == null)
                    {
                        return AnchorResolution.Fail(ChatSearchStatus.LocationNotResolved,
                            $"말씀하신 장소('{analysis.SpecificPlace}')를 정확하게 확인하지 못했어요." +
                            (analysis.Location != null ? $" {analysis.Location} 근처에서 같은 이름의 장소를 찾지 못했어요." : ""));
                    }

                    return AnchorResolution.At(new SearchAnchor(place.Latitude, place.Longitude, place.Label, SearchAnchorSource.SpecificPlace),
                        SpecificPlaceRadiusMeters, place.Place);
                }

                case TravelQueryIntent.NamedLocation:
                {
                    var area = await _locationResolver.ResolveAreaAsync(analysis.Location!, tripCenter ?? gps, cancellationToken);

                    if (area == null)
                    {
                        return AnchorResolution.Fail(ChatSearchStatus.LocationNotResolved,
                            $"'{analysis.Location}' 위치를 정확하게 확인하지 못했어요.");
                    }

                    return AnchorResolution.At(new SearchAnchor(area.Latitude, area.Longitude, area.Label, SearchAnchorSource.NamedLocation),
                        NamedLocationRadiusMeters);
                }

                case TravelQueryIntent.CurrentLocation:
                {
                    if (gps == null)
                    {
                        return AnchorResolution.Fail(ChatSearchStatus.LocationUnavailable,
                            "현재 위치를 확인할 수 없어요. 위치 권한을 허용하시거나, 지역명을 함께 말씀해 주세요.");
                    }

                    return AnchorResolution.At(new SearchAnchor(gps.Value.Latitude, gps.Value.Longitude, "현재 위치", SearchAnchorSource.CurrentLocation),
                        CurrentLocationRadiusMeters);
                }

                default:
                {
                    // 일반 추천: "일정 근처"면 (오늘) 일정 위치, 아니면 여행 일정 전체 지역, 그것도 없으면 현재 위치.
                    if (analysis.RelativeLocation == RelativeLocationKind.NearSchedule)
                    {
                        var scheduleCenter = Centroid(SchedulesForAnchor(schedules, analysis.MentionsToday, asOfUtc));

                        if (scheduleCenter != null)
                        {
                            return AnchorResolution.At(new SearchAnchor(scheduleCenter.Value.Latitude, scheduleCenter.Value.Longitude,
                                analysis.MentionsToday ? "오늘 일정 위치" : "일정 위치", SearchAnchorSource.ScheduleArea), ScheduleAreaRadiusMeters);
                        }
                    }

                    if (tripCenter != null)
                    {
                        return AnchorResolution.At(new SearchAnchor(tripCenter.Value.Latitude, tripCenter.Value.Longitude, "여행 일정 지역", SearchAnchorSource.TripArea),
                            TripAreaRadiusMeters);
                    }

                    if (gps != null)
                    {
                        return AnchorResolution.At(new SearchAnchor(gps.Value.Latitude, gps.Value.Longitude, "현재 위치", SearchAnchorSource.CurrentLocation),
                            CurrentLocationRadiusMeters);
                    }

                    // 기준으로 삼을 위치가 정말 하나도 없을 때만 추가 정보를 묻는다.
                    return AnchorResolution.Fail(ChatSearchStatus.LocationNotResolved,
                        "추천 기준으로 삼을 위치가 없어요. 지역명이나 장소 이름을 함께 말씀해 주세요.");
                }
            }
        }

        // 주변 검색 -> 부속 시설/폐업 제거 -> 거리 계산 -> 중복 제거 -> 거리순 정렬. 후보가 모자라면 반경을 한 번 넓혀 다시 검색한다.
        private async Task<(List<PlaceSearchResultDto> Candidates, int RadiusMeters)> SearchAroundAsync(
            SearchAnchor anchor, int radiusMeters, TravelQueryAnalysis analysis, CancellationToken cancellationToken)
        {
            var candidates = await SearchOnceAsync(anchor, radiusMeters, analysis, cancellationToken);

            int relaxedRadius = Math.Min(radiusMeters * RelaxedRadiusFactor, MaxRelaxedRadiusMeters);

            if (candidates.Count < MinCandidatesBeforeRelaxing && relaxedRadius > radiusMeters)
            {
                var widened = await SearchOnceAsync(anchor, relaxedRadius, analysis, cancellationToken);
                candidates = Deduplicate(candidates.Concat(widened)).ToList();
                radiusMeters = relaxedRadius;
            }

            var ordered = candidates
                .OrderBy(c => c.DistanceMeters ?? double.MaxValue)
                .Take(MaxCandidatesForPrompt)
                .ToList();

            return (ordered, radiusMeters);
        }

        private async Task<List<PlaceSearchResultDto>> SearchOnceAsync(
            SearchAnchor anchor, int radiusMeters, TravelQueryAnalysis analysis, CancellationToken cancellationToken)
        {
            // 메뉴(파스타 등)가 있으면 업종 필터 대신 키워드 검색으로 좁히고, 없으면 세부 업종(한식 등) 또는 카테고리로 검색한다.
            var results = await _placeSearchProvider.SearchNearbyAsync(
                new NearbyPlaceSearchRequest(anchor.Latitude, anchor.Longitude, radiusMeters,
                    Keyword: analysis.MenuKeyword == null && analysis.Category == PlaceCategory.Restaurant ? analysis.CuisineKeyword : null,
                    Category: analysis.Category,
                    MaxResults: RequestedCandidateCount,
                    SearchText: analysis.MenuKeyword),
                cancellationToken);

            foreach (var place in results)
            {
                place.DistanceMeters ??= HaversineTravelTimeProvider.HaversineDistanceMeters(
                    anchor.Latitude, anchor.Longitude, place.Latitude, place.Longitude);

                if (string.IsNullOrWhiteSpace(place.CanonicalName))
                {
                    place.CanonicalName = PlaceNames.ToCanonicalName(place.Name);
                }
            }

            return Deduplicate(results.Where(p =>
                    !PlaceNames.IsFacility(p.Name) &&
                    !p.IsPermanentlyClosed &&
                    p.OperatingStatus != PlaceOperatingStatus.Closed))
                .ToList();
        }

        // 중복 제거: 1) PlaceId 2) 좌표+이름 3) 정규화한 이름+주소. 같은 브랜드의 다른 지점은 이름이 달라 남는다.
        public static IEnumerable<PlaceSearchResultDto> Deduplicate(IEnumerable<PlaceSearchResultDto> places)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var seenCoordinateNames = new HashSet<string>(StringComparer.Ordinal);
            var seenNameAddresses = new HashSet<string>(StringComparer.Ordinal);

            foreach (var place in places)
            {
                string name = NormalizeKey(place.Name);
                string coordinateName = $"{place.Latitude:F5},{place.Longitude:F5}|{name}";
                string nameAddress = $"{name}|{NormalizeKey(place.Address)}";

                bool duplicate =
                    (place.PlaceId.Length > 0 && !seenIds.Add(place.PlaceId)) |
                    !seenCoordinateNames.Add(coordinateName) |
                    (place.Address.Length > 0 && !seenNameAddresses.Add(nameAddress));

                if (!duplicate)
                {
                    yield return place;
                }
            }
        }

        private static string NormalizeKey(string value) => value.Replace(" ", string.Empty).ToLowerInvariant();

        // 일정의 장소명/제목과 이름이 정확히 같은 후보를 뺀다. 이름 일부만 같은 곳('광화문광장' vs '광화문광장 북측광장')은
        // 다른 장소일 수 있어 남긴다. 사용자가 직접 물은 특정 장소(맨 앞에 넣는 기준 장소)는 이 단계 뒤에 넣으므로 빠지지 않는다.
        private static IEnumerable<PlaceSearchResultDto> ExcludeScheduledPlaces(
            IEnumerable<PlaceSearchResultDto> candidates, IReadOnlyList<Schedule> schedules)
        {
            var scheduledNames = schedules
                .SelectMany(s => new[] { s.PlaceName, s.Title })
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            return candidates.Where(c => !scheduledNames.Any(n => PlaceRecommendationGrounder.IsExactName(n, c.Name)));
        }

        private static IEnumerable<Schedule> SchedulesForAnchor(IReadOnlyList<Schedule> schedules, bool todayOnly, DateTime asOfUtc)
        {
            if (!todayOnly)
            {
                return schedules;
            }

            var today = (asOfUtc + LocalUtcOffset).Date;
            var todays = schedules.Where(s => (s.StartTime + LocalUtcOffset).Date == today).ToList();

            // 오늘 일정이 없으면(여행 전/후 등) 전체 일정 위치로 대신한다.
            return todays.Any(s => s.Latitude.HasValue && s.Longitude.HasValue) ? todays : schedules;
        }

        private static (double Latitude, double Longitude)? Centroid(IEnumerable<Schedule> schedules)
        {
            var located = schedules.Where(s => s.Latitude.HasValue && s.Longitude.HasValue).ToList();

            return located.Count == 0
                ? null
                : (located.Average(s => s.Latitude!.Value), located.Average(s => s.Longitude!.Value));
        }

        // 질문 의도에 맞춘 "검색 결과 없음" 문구. 무조건 "더 구체적으로 말해 달라"고 하지 않는다.
        private static string NoCandidatesMessage(TravelQueryAnalysis analysis, SearchAnchor anchor)
        {
            // 메뉴로 좁혀 검색했으면 무엇을 못 찾았는지 메뉴로 알려준다(예: "'파스타' 관련 장소를").
            string what = analysis.MenuKeyword != null
                ? $"'{analysis.MenuKeyword}' 관련 장소를"
                : WithObjectParticle(PlaceCategoryText.ToKorean(analysis.Category));

            return analysis.Intent switch
            {
                TravelQueryIntent.CurrentLocation => $"현재 위치 주변에서 조건에 맞는 {what} 찾지 못했어요.",
                TravelQueryIntent.NamedLocation => $"{analysis.Location} 지역에서 조건에 맞는 {what} 찾지 못했어요.",
                TravelQueryIntent.SpecificPlace => $"{anchor.Label} 주변에서 조건에 맞는 {what} 찾지 못했어요.",
                _ => $"{anchor.Label} 주변에서 조건에 맞는 {what} 찾지 못했어요."
            };
        }

        // 받침 유무에 따라 목적격 조사 '을/를'을 붙인다.
        private static string WithObjectParticle(string word)
        {
            char last = word[^1];
            bool hasFinalConsonant = last >= '가' && last <= '힣' && (last - '가') % 28 != 0;
            return word + (hasFinalConsonant ? "을" : "를");
        }

        // 검색 API 호출 실패(네트워크/HTTP 오류/응답 형식 오류/타임아웃). 호출한 쪽의 취소는 실패로 삼키지 않는다.
        private static bool IsSearchServiceFailure(Exception ex, CancellationToken cancellationToken) =>
            ex is InvalidOperationException or HttpRequestException or JsonException ||
            (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

        private sealed record AnchorResolution(
            SearchAnchor? Anchor, int RadiusMeters, PlaceSearchResultDto? AnchorPlace, CandidateSearchResult? Failure)
        {
            public static AnchorResolution At(SearchAnchor anchor, int radiusMeters, PlaceSearchResultDto? anchorPlace = null) =>
                new(anchor, radiusMeters, anchorPlace, null);

            public static AnchorResolution Fail(ChatSearchStatus status, string message) =>
                new(null, 0, null, new CandidateSearchResult(status, null, 0, [], message));
        }
    }

    // 실제로 검색 중심으로 쓴 위치.
    public sealed record SearchAnchor(double Latitude, double Longitude, string Label, SearchAnchorSource Source);

    // 후보 검색 결과. Status가 Success가 아니면 UserMessage를 그대로 사용자에게 보여주고 LLM은 부르지 않는다.
    // ErrorDetail은 검색 API 오류의 원문(로그용), ExcludedAsScheduled는 이미 일정에 있어 후보에서 뺀 장소 수다.
    public sealed record CandidateSearchResult(
        ChatSearchStatus Status,
        SearchAnchor? Anchor,
        int RadiusMeters,
        IReadOnlyList<PlaceSearchResultDto> Candidates,
        string? UserMessage,
        string? ErrorDetail = null,
        int ExcludedAsScheduled = 0);
}
