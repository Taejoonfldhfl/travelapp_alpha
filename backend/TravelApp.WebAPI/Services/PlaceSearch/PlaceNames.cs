using System.Text.RegularExpressions;

namespace TravelApp.WebAPI.Services.PlaceSearch
{
    // 장소 이름을 비교/표시하기 위한 공용 정규화 도구. provider와 Grounder, LocationResolver가 같은 규칙을 쓴다.
    public static partial class PlaceNames
    {
        // 부속 시설물. 본체 장소 대신 이런 이름이 검색 중심이나 추천 후보가 되면 안 된다(예: '광화문 주차장').
        private static readonly string[] FacilitySuffixes =
            ["주차장", "출구", "입구", "정문", "후문", "남문", "북문", "동문", "서문", "매표소", "화장실", "정류장", "정류소", "ATM"];

        // 검색 API가 이름 뒤에 붙이는 업종 표기('[중식]')를 뗀 표시용 이름. 괄호 안 설명('(휴관중...)')은 이름의 일부일 수 있어 남긴다.
        public static string ToCanonicalName(string name) =>
            BracketTag().Replace(name ?? string.Empty, string.Empty).Trim();

        // 비교용 이름: 업종 표기와 괄호 설명을 뗀다('서촌(북촌한옥마을)' -> '서촌').
        public static string StripDecorations(string name) =>
            ParenthesizedText().Replace(BracketTag().Replace(name ?? string.Empty, string.Empty), string.Empty).Trim();

        public static bool IsFacility(string name)
        {
            string trimmed = StripDecorations(name).Replace(" ", string.Empty);
            return FacilitySuffixes.Any(s => trimmed.EndsWith(s, StringComparison.OrdinalIgnoreCase));
        }

        // 관련성 순위(0부터)를 0~1 신뢰도로. 1순위 1.0, 순위가 하나 내려갈 때마다 0.15씩 낮춘다.
        public static double RankConfidence(int zeroBasedRank) => Math.Max(0.1, 1.0 - zeroBasedRank * 0.15);

        [GeneratedRegex(@"\[[^\]]*\]")]
        private static partial Regex BracketTag();

        [GeneratedRegex(@"\([^)]*\)")]
        private static partial Regex ParenthesizedText();
    }
}
