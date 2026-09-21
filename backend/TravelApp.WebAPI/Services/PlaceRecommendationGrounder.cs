using System;
using SharedData.DTOs;

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

        // asOfUtc(질문한 시점) 기준으로 이 장소를 추천해도 되는지 판단한다: 폐업이 확인됐으면
        // 항상 제외하고, 영업 확인 시점 정보가 있는데 그게 3개월보다 오래됐으면 역시 제외한다.
        // 정보가 아예 없으면(LastConfirmedOperatingDate == null) 방금 이 검색으로 확인된
        // 것 자체를 최신 근거로 보고 통과시킨다.
        public static bool IsOperatingAsOf(PlaceSearchResultDto place, DateTime asOfUtc)
        {
            if (place.IsPermanentlyClosed)
            {
                return false;
            }

            if (place.LastConfirmedOperatingDate == null)
            {
                return true;
            }

            return place.LastConfirmedOperatingDate.Value >= asOfUtc.AddMonths(-MaxOperatingInfoAgeMonths);
        }

        // 추천 하나를 후보 목록과 대조한다. 사용자가 특정 장소를 언급해 후보 목록 밖의 이름이
        // 나온 경우, 호출한 쪽(AiChatController)이 이 결과를 보고 장소 검색 API의 이름 검색으로
        // 한 번 더 검증할 수 있도록 매치 여부를 그대로 돌려준다.
        public static bool TryMatch(
            string placeName, IReadOnlyList<PlaceSearchResultDto> candidates, out PlaceSearchResultDto? match)
        {
            match = FindMatch(placeName, candidates);
            return match != null;
        }

        private static PlaceSearchResultDto? FindMatch(
            string placeName, IReadOnlyList<PlaceSearchResultDto> candidates)
        {
            string normalizedName = Normalize(placeName);

            if (normalizedName.Length == 0)
            {
                return null;
            }

            foreach (var candidate in candidates)
            {
                string normalizedCandidate = Normalize(candidate.Name);

                if (normalizedCandidate.Length == 0)
                {
                    continue;
                }

                bool isSameOrSubstring =
                    normalizedName == normalizedCandidate ||
                    (normalizedCandidate.Length >= 2 && normalizedName.Contains(normalizedCandidate)) ||
                    (normalizedName.Length >= 2 && normalizedCandidate.Contains(normalizedName));

                if (isSameOrSubstring)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string Normalize(string value) =>
            value.Replace(" ", "").Trim().ToLowerInvariant();
    }
}
