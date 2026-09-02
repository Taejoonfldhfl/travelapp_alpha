package com.example.testbuild01.ui.aichat

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import com.example.testbuild01.BuildConfig
import com.example.testbuild01.data.model.AiChatRequest
import com.example.testbuild01.data.model.AiChatResponse
import com.example.testbuild01.data.model.AiChatSessionResponse
import com.example.testbuild01.data.model.ScheduleCreateRequest
import com.example.testbuild01.data.model.ScheduleResponse
import com.example.testbuild01.data.network.RetrofitClient
import com.example.testbuild01.data.repository.PlacesRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

class AiChatViewModel(
    private val tripId: Int,
    private val placesRepository: PlacesRepository = PlacesRepository(BuildConfig.google_maps_api_key)
) : ViewModel() {

    private val _messages = MutableStateFlow<List<ChatMessage>>(emptyList())
    val messages: StateFlow<List<ChatMessage>> = _messages.asStateFlow()

    private val _isSending = MutableStateFlow(false)
    val isSending: StateFlow<Boolean> = _isSending.asStateFlow()

    private var cardSequence = 0
    private var sessionId: Int? = null

    fun sendMessage(text: String) {
        if (text.isBlank() || _isSending.value) return

        _messages.update { it + ChatMessage.UserMessage(text) }
        _isSending.value = true

        val existingSessionId = sessionId
        if (existingSessionId != null) {
            postMessage(existingSessionId, text)
        } else {
            createSessionThenSend(text)
        }
    }

    private fun createSessionThenSend(text: String) {
        RetrofitClient.instance
            .createAiChatSession(tripId)
            .enqueue(object : Callback<AiChatSessionResponse> {
                override fun onResponse(
                    call: Call<AiChatSessionResponse>,
                    response: Response<AiChatSessionResponse>
                ) {
                    val body = response.body()

                    if (response.isSuccessful && body != null) {
                        sessionId = body.sessionId
                        postMessage(body.sessionId, text)
                    } else {
                        _isSending.value = false
                        _messages.update {
                            it + ChatMessage.AiMessage("채팅 세션을 시작하지 못했어요. (${response.code()})")
                        }
                    }
                }

                override fun onFailure(call: Call<AiChatSessionResponse>, t: Throwable) {
                    _isSending.value = false
                    _messages.update {
                        it + ChatMessage.AiMessage("서버 연결에 실패했어요.")
                    }
                }
            })
    }

    private fun postMessage(sessionId: Int, text: String) {
        RetrofitClient.instance
            .sendAiChatMessage(tripId, sessionId, AiChatRequest(text))
            .enqueue(object : Callback<AiChatResponse> {
                override fun onResponse(
                    call: Call<AiChatResponse>,
                    response: Response<AiChatResponse>
                ) {
                    _isSending.value = false
                    val body = response.body()

                    if (response.isSuccessful && body != null) {
                        _messages.update { it + ChatMessage.AiMessage(body.replyText) }

                        body.recommendations.forEach { recommendation ->
                            val cardId = "card_${cardSequence++}"

                            _messages.update {
                                it + ChatMessage.RecommendationCard(
                                    id = cardId,
                                    placeName = recommendation.placeName,
                                    description = recommendation.description,
                                    suggestedStartTime = recommendation.suggestedStartTime,
                                    suggestedEndTime = recommendation.suggestedEndTime
                                )
                            }

                            loadPhoto(cardId, recommendation.placeName)
                        }
                    } else {
                        _messages.update {
                            it + ChatMessage.AiMessage("추천을 가져오지 못했어요. (${response.code()})")
                        }
                    }
                }

                override fun onFailure(call: Call<AiChatResponse>, t: Throwable) {
                    _isSending.value = false
                    _messages.update {
                        it + ChatMessage.AiMessage("서버 연결에 실패했어요.")
                    }
                }
            })
    }

    private fun loadPhoto(cardId: String, placeName: String) {
        placesRepository.fetchPlacePhotoUrl(placeName) { photoUrl ->
            updateCard(cardId) { it.copy(photoUrl = photoUrl, photoLoading = false) }
        }
    }

    fun addToSchedule(
        card: ChatMessage.RecommendationCard,
        startTime: String,
        endTime: String,
        onResult: (success: Boolean, errorMessage: String?) -> Unit
    ) {
        val request = ScheduleCreateRequest(
            title = card.placeName,
            placeName = card.placeName,
            description = card.description,
            startTime = startTime,
            endTime = endTime,
            order = 0
        )

        RetrofitClient.instance
            .createSchedule(tripId, request)
            .enqueue(object : Callback<ScheduleResponse> {
                override fun onResponse(
                    call: Call<ScheduleResponse>,
                    response: Response<ScheduleResponse>
                ) {
                    if (response.isSuccessful) {
                        updateCard(card.id) { it.copy(addedToSchedule = true) }
                        onResult(true, null)
                    } else {
                        onResult(false, "일정 추가 실패 (${response.code()})")
                    }
                }

                override fun onFailure(call: Call<ScheduleResponse>, t: Throwable) {
                    onResult(false, "서버 연결에 실패했어요.")
                }
            })
    }

    private fun updateCard(
        cardId: String,
        transform: (ChatMessage.RecommendationCard) -> ChatMessage.RecommendationCard
    ) {
        _messages.update { list ->
            list.map { message ->
                if (message is ChatMessage.RecommendationCard && message.id == cardId) {
                    transform(message)
                } else {
                    message
                }
            }
        }
    }
}

class AiChatViewModelFactory(
    private val tripId: Int
) : ViewModelProvider.Factory {
    override fun <T : ViewModel> create(modelClass: Class<T>): T {
        @Suppress("UNCHECKED_CAST")
        return AiChatViewModel(tripId) as T
    }
}
