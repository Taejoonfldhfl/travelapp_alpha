using System.Text.Json.Serialization;

namespace SharedData.DTOs
{
    // AI 챗봇 응답이 어떤 상태로 끝났는지. 클라이언트가 "검색 결과 없음"과 "검색 서비스 오류"를 구분할 수 있게 한다.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ChatSearchStatus
    {
        Success,                // 후보를 찾았고 LLM 추천이 검증을 통과했다(추천 0건일 수도 있음 — LLM이 어울리는 곳이 없다고 판단)
        NoCandidates,           // 검색 기준점은 정했지만 조건에 맞는 장소가 없었다
        LocationNotResolved,    // 사용자가 말한 지역/장소의 좌표를 확정하지 못했다
        LocationUnavailable,    // "근처"를 물었지만 현재 위치(GPS)가 없다
        SearchUnavailable,      // 장소 검색 API 호출 자체가 실패했다
        GroundingFailed,        // LLM이 추천을 냈지만 모두 후보 목록에서 확인되지 않아 제외됐다
        NotRecommendation       // 장소 추천 요청이 아니었다(예: "오늘 일정 알려줘")
    }
}
