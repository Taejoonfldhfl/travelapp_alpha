using System;
using SharedData.DTOs;
using TravelApp.WebAPI.Services.PlaceSearch;

namespace TravelApp.WebAPI.Services
{
    // 규칙 2 집행부: AI가 만든 추천 목록을, 장소 검색 API(주변 카테고리 검색)가 실제로 반환한
    // "좌표가 확정된 장소 후보 목록"과 대조한다. 후보 목록의 장소와 이름이 일치하지 않거나,
    // 일치해도 폐업했거나 정보가 오래된 장소는 (규칙 2 위반 또는 신뢰할 수 없는 추천이므로)
    // 전부 버린다. 살아남은 추천에는 후보의 PlaceId/Latitude/Longitude를 그대로 붙여 반환한다.
    public static class PlaceRecommendationGrounder
    {
        // 영업 정보가 이 기간보다 오래됐으면(최근 확인된 적이 없으면) 신뢰하지 않는다.
        private const int MaxOperatingInfoAgeMonths = 3;

        public static List<AiPlaceRecommendationDto> Ground(
            IEnumerable<AiPlaceRecommendationDto> rawRecommendations,
            IReadOnlyList<PlaceSearchResultDto> candidates)
        {
            var grounded = new List<AiPlaceRecommendationDto>();
            var now = DateTime.UtcNow;

            foreach (var recommendation in rawRecommendations)
            {
                if (!TryMatch(recommendation.PlaceName, candidates, out var match) ||
                    !IsOperatingAsOf(match!, now))
                {
                    // 후보 목록에 없는 장소(좌표를 확정할 수 없음) 또는 폐업/오래된 정보 -> 차단.
                    continue;
                }

                recommendation.PlaceId = match!.PlaceId;
                recommendation.Latitude = match.Latitude;
                recommendation.Longitude = match.Longitude;
                grounded.Add(recommendation);
            }

            return grounded;
        }

        // 후보 목록에 없으면 이름 검색으로 한 번 더 확인하는 검증 흐름. 추천마다 어느 단계에서 채택/제외됐는지를 함께 돌려준다.
        // (AI 챗봇은 이제 GroundToCandidates로 후보 목록 안에서만 검증한다 — 이 메서드는 후보 밖 장소를 이름으로
        // 확인해야 하는 다른 용도를 위해 남겨 둔다.)
        // 1) 주변 후보 목록과 이름이 일치하고 영업 중이면 채택.
        // 2) 아니면 이름 검색으로 다시 찾는다: 못 찾으면 제외, 찾았어도 이름이 추천과 다르면 제외
        //    (Tmap 이름 검색은 검색어와 무관한 첫 번째 POI를 돌려줄 수 있어서, 지어낸 이름이 엉뚱한
        //    실제 장소의 좌표를 달고 통과하는 걸 막는다), 폐업/오래된 정보면 제외, 나머지는 채택.
        public static async Task<List<GroundingOutcome>> GroundAsync(
            IEnumerable<AiPlaceRecommendationDto> rawRecommendations,
            IReadOnlyList<PlaceSearchResultDto> candidates,
            INearbyPlaceSearchProvider placeSearchProvider,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default)
        {
            var outcomes = new List<GroundingOutcome>();

            foreach (var recommendation in rawRecommendations)
            {
                bool candidateMatched = TryMatch(recommendation.PlaceName, candidates, out var candidateMatch);

                if (candidateMatched && IsOperatingAsOf(candidateMatch!, asOfUtc))
                {
                    Attach(recommendation, candidateMatch!);
                    outcomes.Add(new GroundingOutcome(recommendation, GroundingStage.CandidateMatched, candidateMatched, candidateMatch!.Name));
                    continue;
                }

                var searchResults = await placeSearchProvider.SearchByNameAsync(recommendation.PlaceName, cancellationToken);
                outcomes.Add(PickFromNameSearch(recommendation, searchResults, candidateMatched, asOfUtc));
            }

            return outcomes;
        }

        // AiChatController가 쓰는 검증: LLM 추천을 이번 검색 후보 목록 "안에서만" 확인한다(후보 밖 이름은 이름 검색으로
        // 되살리지 않는다 — 검색 범위는 LLM이 아니라 서버가 정한다). 매칭 우선순위:
        //  1) placeId(프롬프트에 준 후보 별칭 c1, c2... 또는 실제 PlaceId) 정확 일치. 목록에 없는 placeId면 제외하고,
        //     placeId는 맞지만 함께 준 이름이 그 후보와 다른 장소면 LLM이 지점을 헷갈린 것으로 보고 제외한다.
        //  2) placeId가 없으면 이름으로: 정확히 같은 이름 -> 지점명만 붙은 이름 -> 아주 비슷한 이름 순이고,
        //     같은 단계에서 후보가 둘 이상이면(예: '스타벅스' vs 여러 지점) 어느 지점인지 모르므로 제외한다.
        // 통과한 추천에는 후보의 실제 PlaceId/좌표/지점까지 구분되는 이름/영업 상태를 붙인다.
        public static List<GroundingOutcome> GroundToCandidates(
            IEnumerable<AiPlaceRecommendationDto> rawRecommendations,
            IReadOnlyList<PlaceSearchResultDto> candidates,
            DateTime asOfUtc)
        {
            var outcomes = new List<GroundingOutcome>();

            foreach (var recommendation in rawRecommendations)
            {
                PlaceSearchResultDto? candidate;
                GroundingStage matchedStage;

                if (!string.IsNullOrWhiteSpace(recommendation.PlaceId))
                {
                    candidate = FindByPlaceId(recommendation.PlaceId, candidates);

                    if (candidate == null)
                    {
                        outcomes.Add(new GroundingOutcome(recommendation, GroundingStage.UnknownPlaceId, false, null));
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(recommendation.PlaceName) &&
                        !IsSufficientlySimilar(recommendation.PlaceName, candidate.Name))
                    {
                        outcomes.Add(new GroundingOutcome(recommendation, GroundingStage.PlaceIdNameMismatch, true, candidate.Name));
                        continue;
                    }

                    matchedStage = GroundingStage.PlaceIdMatched;
                }
                else
                {
                    var (match, ambiguous) = MatchByName(recommendation.PlaceName, candidates);

                    if (match == null)
                    {
                        outcomes.Add(new GroundingOutcome(recommendation,
                            ambiguous ? GroundingStage.AmbiguousName : GroundingStage.NotInCandidates, false, null));
                        continue;
                    }

                    candidate = match;
                    matchedStage = GroundingStage.CandidateMatched;
                }

                if (!IsOperatingAsOf(candidate, asOfUtc))
                {
                    outcomes.Add(new GroundingOutcome(recommendation, GroundingStage.NotOperating, true, candidate.Name));
                    continue;
                }

                Attach(recommendation, candidate);
                recommendation.PlaceName = DisplayName(candidate);
                recommendation.OperatingStatus = GetOperatingStatus(candidate, asOfUtc);
                outcomes.Add(new GroundingOutcome(recommendation, matchedStage, true, candidate.Name));
            }

            return outcomes;
        }

        // 프롬프트에서 후보를 가리키는 짧은 별칭. 긴 실제 PlaceId를 LLM이 옮겨 적다 틀리는 걸 막는다.
        public static string CandidateAlias(int zeroBasedIndex) => $"c{zeroBasedIndex + 1}";

        private static PlaceSearchResultDto? FindByPlaceId(string placeId, IReadOnlyList<PlaceSearchResultDto> candidates)
        {
            string id = placeId.Trim();

            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(CandidateAlias(i), id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidates[i].PlaceId, id, StringComparison.Ordinal))
                {
                    return candidates[i];
                }
            }

            return null;
        }

        private static string DisplayName(PlaceSearchResultDto place) =>
            string.IsNullOrWhiteSpace(place.CanonicalName) ? place.Name : place.CanonicalName;

        // 이름 검색 결과를 관련성 순서대로 훑어, 유사도와 영업 여부를 "둘 다" 통과하는 첫 후보를 채택한다.
        // 유사도는 통과했지만 폐업/오래된 정보인 후보는 채택하지 않고 다음 순위로 넘어간다(같은 이름의 다른
        // 지점이 영업 중일 수 있다). 끝까지 통과하는 후보가 없으면 제외하고, 사유는 유사도를 통과한 후보가
        // 하나라도 있었으면 영업여부 실패, 없었으면 유사도 실패, 결과가 아예 없었으면 이름검색 실패다.
        private static GroundingOutcome PickFromNameSearch(
            AiPlaceRecommendationDto recommendation,
            IReadOnlyList<PlaceSearchResultDto> searchResults,
            bool candidateMatched,
            DateTime asOfUtc)
        {
            var searchedNames = searchResults.Select(r => r.Name).ToList();

            if (searchResults.Count == 0)
            {
                return new GroundingOutcome(recommendation, GroundingStage.NameSearchNotFound, candidateMatched, null);
            }

            PlaceSearchResultDto? firstSimilarButNotOperating = null;

            for (int i = 0; i < searchResults.Count; i++)
            {
                var result = searchResults[i];

                if (!IsSufficientlySimilar(recommendation.PlaceName, result.Name))
                {
                    continue;
                }

                if (!IsOperatingAsOf(result, asOfUtc))
                {
                    firstSimilarButNotOperating ??= result;
                    continue;
                }

                Attach(recommendation, result);
                return new GroundingOutcome(recommendation, GroundingStage.NameSearchMatched, candidateMatched, result.Name,
                    MatchedRank: i + 1, SearchedNames: searchedNames);
            }

            return firstSimilarButNotOperating != null
                ? new GroundingOutcome(recommendation, GroundingStage.NotOperating, candidateMatched, firstSimilarButNotOperating.Name,
                    SearchedNames: searchedNames)
                : new GroundingOutcome(recommendation, GroundingStage.NameSimilarityFailed, candidateMatched, null,
                    SearchedNames: searchedNames);
        }

        private static void Attach(AiPlaceRecommendationDto recommendation, PlaceSearchResultDto place)
        {
            recommendation.PlaceId = place.PlaceId;
            recommendation.Latitude = place.Latitude;
            recommendation.Longitude = place.Longitude;
        }

        // asOfUtc(질문한 시점) 기준으로 이 장소를 추천 후보로 써도 되는지: 폐업이 확인됐으면 항상 제외하고,
        // 영업 확인 시점 정보가 있는데 3개월보다 오래됐으면 역시 제외한다. 영업 정보가 아예 없으면(Unknown)
        // 후보에서 빼지는 않는다 — 단, 이는 "영업 중"이라는 뜻이 아니다(GetOperatingStatus는 Unknown을 돌려준다).
        // TODO(향후 작업): Tmap POI 응답에는 휴관/임시휴업 여부 필드가 없어 IsOperatingAsOf가
        // 이를 감지하지 못함(예: 국립한글박물관 휴관 사례). 별도 데이터 소스(예: 네이버 플레이스,
        // 공공API) 연동이 필요한 별개 작업으로 분리함.
        public static bool IsOperatingAsOf(PlaceSearchResultDto place, DateTime asOfUtc)
        {
            if (place.IsPermanentlyClosed || place.OperatingStatus == PlaceOperatingStatus.Closed)
            {
                return false;
            }

            if (place.LastConfirmedOperatingDate == null)
            {
                return true;
            }

            return place.LastConfirmedOperatingDate.Value >= asOfUtc.AddMonths(-MaxOperatingInfoAgeMonths);
        }

        // 사용자/LLM에게 알릴 영업 상태. 최근 3개월 안에 영업이 확인됐거나 provider가 Open이라고 한 경우만 Open이고,
        // 확인할 수 없으면 Unknown이다(Unknown != Open).
        public static PlaceOperatingStatus GetOperatingStatus(PlaceSearchResultDto place, DateTime asOfUtc)
        {
            if (place.IsPermanentlyClosed || place.OperatingStatus == PlaceOperatingStatus.Closed)
            {
                return PlaceOperatingStatus.Closed;
            }

            if (place.LastConfirmedOperatingDate != null)
            {
                return place.LastConfirmedOperatingDate.Value >= asOfUtc.AddMonths(-MaxOperatingInfoAgeMonths)
                    ? PlaceOperatingStatus.Open
                    : PlaceOperatingStatus.Unknown;
            }

            return place.OperatingStatus;
        }

        // 추천 하나를 후보 목록과 대조한다. 같은 단계에서 후보가 둘 이상 걸리면(어느 지점인지 모름) 매치 실패로 본다.
        public static bool TryMatch(
            string placeName, IReadOnlyList<PlaceSearchResultDto> candidates, out PlaceSearchResultDto? match)
        {
            match = MatchByName(placeName, candidates).Match;
            return match != null;
        }

        // 1) 정확히 같은 이름(공백/대소문자/행정단위/업종 표기 차이만 무시) -> 목록 순서와 상관없이 우선.
        //    예: '광화문광장 북측광장'이 목록 앞쪽의 '광화문'(광화문 정문)이 아니라 같은 이름 후보에 붙는다.
        // 2) 그다음 지점명만 붙은 이름이나 아주 비슷한 이름(IsSufficientlySimilar).
        // 같은 단계의 후보가 여럿이면 Ambiguous.
        private static (PlaceSearchResultDto? Match, bool Ambiguous) MatchByName(
            string placeName, IReadOnlyList<PlaceSearchResultDto> candidates)
        {
            if (NormalizeForComparison(placeName).Length == 0)
            {
                return (null, false);
            }

            var exact = candidates.Where(c => IsExactName(placeName, c.Name)).ToList();

            if (exact.Count > 0)
            {
                return exact.Count == 1 ? (exact[0], false) : (null, true);
            }

            var similar = candidates.Where(c => IsSufficientlySimilar(placeName, c.Name)).ToList();

            return similar.Count switch
            {
                0 => (null, false),
                1 => (similar[0], false),
                _ => (null, true)
            };
        }

        // 행정단위의 긴 표기를 짧은 표기로 맞춘다('서울특별시청' -> '서울시청'). 통째로 지우면
        // '서울특별시청'이 '서울청'이 되어 '서울시청'과 여전히 안 맞으므로 기본 단위('시'/'도')로 바꾼다.
        // 긴 접미어부터 바꿔야 '특별자치시'가 '특별시'보다 먼저 처리된다.
        private static readonly (string Long, string Short)[] AdministrativeSuffixes =
        [
            ("특별자치시", "시"),
            ("특별자치도", "도"),
            ("특별시", "시"),
            ("광역시", "시"),
        ];

        // '~점'으로 끝나도 지점이 아니라 업종인 말들. '몽로' + '주점'은 몽로의 지점이 아니다.
        private static readonly string[] NonBranchSuffixes =
            ["주점", "음식점", "편의점", "서점", "상점", "매점", "할인점", "전문점", "판매점", "대리점", "가맹점", "백화점"];

        // 오타·표기 차이 수준으로만 다를 때 같은 장소로 보는 문자열 유사도 하한. 짧은 이름은 한두 글자 차이도
        // 다른 가게일 수 있어 이 규칙을 쓰지 않는다.
        private const double MinStringSimilarity = 0.85;
        private const int MinLengthForFuzzyMatch = 4;

        public static bool IsExactName(string placeName, string otherName)
        {
            string normalizedName = NormalizeForComparison(placeName);
            return normalizedName.Length > 0 && normalizedName == NormalizeForComparison(otherName);
        }

        // 같은 장소 이름으로 볼 수 있는지. 예전의 "한쪽이 다른 쪽을 포함하면 같다" 규칙은 '광화문'이 '우육면당 광화문점'에,
        // 'ABC'가 'ABC마트'에 걸리는 약한 매칭을 통과시켜서, 다음 세 경우만 인정한다.
        //  1) 정확히 같은 이름(공백/대소문자/행정단위 표기/업종 표기 '[중식]'/괄호 설명 차이는 무시)
        //  2) 짧은 쪽 이름 뒤에 지점명만 붙은 경우('몽로' vs '몽로 광화문점', '명동교자' vs '명동교자 본점')
        //  3) 둘 다 4글자 이상이고 문자열 유사도가 0.85 이상(오타 수준 차이)
        public static bool IsSufficientlySimilar(string placeName, string otherName)
        {
            string normalizedName = NormalizeForComparison(placeName);
            string normalizedOther = NormalizeForComparison(otherName);

            if (normalizedName.Length == 0 || normalizedOther.Length == 0)
            {
                return false;
            }

            if (normalizedName == normalizedOther)
            {
                return true;
            }

            var (shorter, longer) = normalizedName.Length <= normalizedOther.Length
                ? (normalizedName, normalizedOther)
                : (normalizedOther, normalizedName);

            if (shorter.Length >= 2 && longer.StartsWith(shorter, StringComparison.Ordinal) &&
                IsBranchSuffix(longer[shorter.Length..]))
            {
                return true;
            }

            return shorter.Length >= MinLengthForFuzzyMatch &&
                StringSimilarity(normalizedName, normalizedOther) >= MinStringSimilarity;
        }

        private static bool IsBranchSuffix(string remainder) =>
            remainder.Length >= 2 &&
            remainder.EndsWith("점", StringComparison.Ordinal) &&
            !NonBranchSuffixes.Any(s => remainder.EndsWith(s, StringComparison.Ordinal));

        // 1 - (편집 거리 / 긴 쪽 길이).
        private static double StringSimilarity(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];

            for (int j = 0; j <= b.Length; j++)
            {
                previous[j] = j;
            }

            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;

                for (int j = 1; j <= b.Length; j++)
                {
                    int substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                    current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), substitution);
                }

                (previous, current) = (current, previous);
            }

            return 1.0 - (double)previous[b.Length] / Math.Max(a.Length, b.Length);
        }

        private static string Normalize(string value) =>
            value.Replace(" ", "").Trim().ToLowerInvariant();

        private static string NormalizeForComparison(string value) =>
            NormalizeAdministrativeSuffixes(Normalize(PlaceNames.StripDecorations(value)));

        private static string NormalizeAdministrativeSuffixes(string value)
        {
            foreach (var (longForm, shortForm) in AdministrativeSuffixes)
            {
                value = value.Replace(longForm, shortForm);
            }

            return value;
        }
    }
}
