using System.Text.Json.Serialization;

namespace SharedData.DTOs
{
    // 장소의 영업 상태. Unknown은 "영업 중"이 아니다 — 검색 API가 영업 정보를 주지 않으면(예: Tmap) 항상 Unknown이고,
    // 이 경우 추천 후보에서 빼지는 않지만 영업 중이라고 표현해서도 안 된다.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PlaceOperatingStatus
    {
        Unknown,
        Open,
        Closed
    }
}
