package com.example.testbuild01.data.model

data class AiChatRequest(
    val message: String,
    // "근처/주변" 표현일 때만 채운다. 위치 권한이 없거나 GPS를 못 가져오면 null로 보낸다.
    val currentLatitude: Double? = null,
    val currentLongitude: Double? = null
)

// imageUrl은 서버의 사진 provider(IPlaceImageProvider)가 채운 대표 사진 URL이다(LLM이 만든 값이 아님).
// null이면 placeName으로 PlacesRepository를 통해 프론트에서 별도로 사진을 조회하고,
// 그래도 없으면 카드에 "사진 없음" 플레이스홀더를 보여준다.
//
// placeId/latitude/longitude는 서버가 장소 검색 API 결과와 대조해 좌표를 확정한
// 장소에만 채워진다. 좌표를 확정하지 못한 추천은 서버에서 이미 걸러지므로,
// 이 목록에 오는 항목은 항상 latitude/longitude가 채워져 있다고 볼 수 있다.
data class AiPlaceRecommendation(
    val placeName: String,
    val description: String,
    val suggestedStartTime: String,
    val suggestedEndTime: String,
    val placeId: String? = null,
    val latitude: Double? = null,
    val longitude: Double? = null,
    val imageUrl: String? = null
)

data class AiChatResponse(
    val replyText: String,
    val recommendations: List<AiPlaceRecommendation>
)

// AI 챗봇 세션 생성 응답. 이후 메시지 전송 시 sessionId를 계속 사용한다.
data class AiChatSessionResponse(
    val sessionId: Int,
    val tripId: Int,
    val createdAt: String
)
