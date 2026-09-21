using SharedData.DTOs;

namespace TravelApp.WebAPI.Services
{
    // 규칙 2 집행부: AI가 만든 추천 목록을, 장소 검색 API(주변 카테고리 검색)가 실제로 반환한
    // "좌표가 확정된 장소 후보 목록"과 대조한다. 후보 목록의 장소와 이름이 일치하지 않는 추천은
    // (좌표를 하나로 특정할 수 없는, 뭉뚱그린 답변이라는 뜻이므로) 전부 버린다.
    // 살아남은 추천에는 후보의 PlaceId/Latitude/Longitude를 그대로 붙여 반환한다.
    public static class PlaceRecommendationGrounder
    {
        public static List<AiPlaceRecommendationDto> Ground(
            IEnumerable<AiPlaceRecommendationDto> rawRecommendations,
            IReadOnlyList<PlaceSearchResultDto> candidates)
        {
            var grounded = new List<AiPlaceRecommendationDto>();

            foreach (var recommendation in rawRecommendations)
            {
                if (!TryMatch(recommendation.PlaceName, candidates, out var match))
                {
                    // 후보 목록에 없는 장소 = 좌표를 확정할 수 없는 답변 -> 차단.
                    continue;
                }

                recommendation.PlaceId = match!.PlaceId;
                recommendation.Latitude = match.Latitude;
                recommendation.Longitude = match.Longitude;
                grounded.Add(recommendation);
            }

            return grounded;
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
