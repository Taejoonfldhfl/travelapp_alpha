package com.example.testbuild01.ui.aichat

// AI 챗봇 화면에 표시되는 항목들. 채팅 UI 화면/ViewModel 전용 상태이며 네트워크 DTO와는 별개다.
sealed class ChatMessage {

    data class UserMessage(
        val text: String
    ) : ChatMessage()

    data class AiMessage(
        val text: String
    ) : ChatMessage()

    data class RecommendationCard(
        val id: String,
        val placeName: String,
        val description: String,
        val suggestedStartTime: String,
        val suggestedEndTime: String,
        val photoUrl: String? = null,
        val photoLoading: Boolean = true,
        val addedToSchedule: Boolean = false,
        val latitude: Double? = null,
        val longitude: Double? = null
    ) : ChatMessage()
}
