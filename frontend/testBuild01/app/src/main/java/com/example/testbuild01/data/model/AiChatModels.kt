package com.example.testbuild01.data.model

data class AiChatRequest(
    val message: String
)

// 서버 응답에는 사진이 절대 포함되지 않는다.
// placeName으로 PlacesRepository를 통해 프론트에서 별도로 사진을 조회한다.
data class AiPlaceRecommendation(
    val placeName: String,
    val description: String,
    val suggestedStartTime: String,
    val suggestedEndTime: String
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
