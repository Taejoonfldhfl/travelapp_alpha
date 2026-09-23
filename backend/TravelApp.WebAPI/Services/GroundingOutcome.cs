using SharedData.DTOs;

namespace TravelApp.WebAPI.Services
{
    // 추천 하나가 그라운딩의 어느 단계에서 채택/제외됐는지.
    public enum GroundingStage
    {
        CandidateMatched,      // 채택: 주변 후보 목록과 일치
        NameSearchMatched,     // 채택: 이름 검색으로 확인
        NameSearchNotFound,    // 제외: 후보 목록에도 없고 이름 검색도 실패
        NameSimilarityFailed,  // 제외: 이름 검색 결과 중 추천한 이름과 같은 장소가 하나도 없음
        NotOperating,          // 제외: 이름이 같은 결과는 있었지만 모두 폐업이거나 영업 정보가 3개월보다 오래됨

        // 아래는 후보 목록 안에서만 검증하는 GroundToCandidates의 단계들.
        PlaceIdMatched,        // 채택: LLM이 준 placeId가 후보와 일치
        UnknownPlaceId,        // 제외: LLM이 준 placeId가 후보 목록에 없음(지어낸 id)
        PlaceIdNameMismatch,   // 제외: placeId는 후보에 있지만 함께 준 이름이 그 후보와 다른 장소(지점 혼동)
        NotInCandidates,       // 제외: placeId 없이 준 이름이 후보 목록에 없음
        AmbiguousName          // 제외: 이름이 여러 후보(여러 지점)에 걸려 어느 곳인지 특정할 수 없음
    }

    // CandidateMatched는 후보 목록과 이름은 일치했는지(영업 여부 실패로 이름 검색까지 갔을 수 있음),
    // FoundName은 최종 판정에 쓰인 장소 이름(후보 또는 이름 검색 결과, 해당 없으면 null),
    // MatchedRank는 이름 검색 결과 중 몇 순위가 채택됐는지, SearchedNames는 이름 검색이 돌려준 이름들(관련성 순)이다.
    public sealed record GroundingOutcome(
        AiPlaceRecommendationDto Recommendation,
        GroundingStage Stage,
        bool CandidateMatched,
        string? FoundName,
        int? MatchedRank = null,
        IReadOnlyList<string>? SearchedNames = null)
    {
        public bool Accepted => Stage is GroundingStage.CandidateMatched or GroundingStage.NameSearchMatched or GroundingStage.PlaceIdMatched;

        public string Reason => Stage switch
        {
            GroundingStage.CandidateMatched => $"후보목록 일치 ('{FoundName}')",
            GroundingStage.NameSearchMatched => $"{CandidatePrefix} → 이름검색 일치 ('{FoundName}', {MatchedRank}순위)",
            GroundingStage.NameSearchNotFound => $"{CandidatePrefix} → 이름검색 실패",
            GroundingStage.NameSimilarityFailed => $"{CandidatePrefix} → 유사도 실패 (이름검색 상위 {SearchedNames?.Count ?? 0}건: {QuotedSearchedNames})",
            GroundingStage.NotOperating when SearchedNames == null => $"후보목록 일치했으나 영업여부 실패 ('{FoundName}')",
            GroundingStage.NotOperating => $"{CandidatePrefix} → 영업여부 실패 ('{FoundName}')",
            GroundingStage.PlaceIdMatched => $"placeId 일치 ('{FoundName}')",
            GroundingStage.UnknownPlaceId => $"후보목록에 없는 placeId ('{Recommendation.PlaceId}')",
            GroundingStage.PlaceIdNameMismatch => $"placeId는 '{FoundName}'인데 이름이 다름",
            GroundingStage.NotInCandidates => "후보목록에 없는 이름",
            GroundingStage.AmbiguousName => "이름이 여러 후보(지점)에 걸려 특정 불가",
            _ => Stage.ToString()
        };

        private string QuotedSearchedNames => string.Join(", ", (SearchedNames ?? []).Select(n => $"'{n}'"));

        private string CandidatePrefix => CandidateMatched ? "후보목록 일치했으나 영업여부 실패" : "후보목록 불일치";
    }
}
