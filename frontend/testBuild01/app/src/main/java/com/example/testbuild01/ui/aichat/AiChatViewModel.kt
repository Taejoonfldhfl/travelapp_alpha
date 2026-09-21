package com.example.testbuild01.ui.aichat

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import androidx.core.content.ContextCompat
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
import com.google.android.gms.location.LocationServices
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

class AiChatViewModel(
    private val tripId: Int,
    private val appContext: Context,
    private val placesRepository: PlacesRepository = PlacesRepository(BuildConfig.google_maps_api_key)
) : ViewModel() {

    private val _messages = MutableStateFlow<List<ChatMessage>>(emptyList())
    val messages: StateFlow<List<ChatMessage>> = _messages.asStateFlow()

    private val _isSending = MutableStateFlow(false)
    val isSending: StateFlow<Boolean> = _isSending.asStateFlow()

    private var cardSequence = 0
    private var sessionId: Int? = null

    // 위치 정보 수집 동의 다이얼로그를 지금 보여줘야 하는지 나타내는 상태.
    // (SharedFlow 1회성 이벤트로 만들었다가, ViewModel 생성(init) 시점이 화면의
    // LaunchedEffect가 구독을 시작하는 시점보다 빨라서 이벤트가 유실되는 문제가 있었다 —
    // replay가 없는 SharedFlow는 "아직 아무도 구독하지 않은 상태"에서 보낸 값을 보관해주지
    // 않는다. StateFlow는 항상 "현재 값"을 유지하다가 구독이 시작되는 순간 그 값을 즉시
    // 전달하므로 이 타이밍 문제가 생기지 않는다.)
    private val _showLocationConsentDialog = MutableStateFlow(false)
    val showLocationConsentDialog: StateFlow<Boolean> = _showLocationConsentDialog.asStateFlow()

    // 사용자가 동의했는데 시스템 위치 권한이 아직 없을 때, 화면(Composable)에 런타임 권한 요청을
    // 대신 실행해달라고 알리는 1회성 이벤트. 실제 권한 다이얼로그는 Activity 컨텍스트가 필요한
    // rememberLauncherForActivityResult에서만 띄울 수 있어 ViewModel이 직접 하지 않는다.
    private val _locationPermissionRequests = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val locationPermissionRequests: SharedFlow<Unit> = _locationPermissionRequests.asSharedFlow()

    // 이 채팅 세션에서 확보된 위치(동의+권한+GPS 성공 시). 못 구했다면(동의 거부/권한 거부/
    // GPS 실패) 둘 다 null로 남고, 매 메시지가 그 값을 그대로 재사용한다. 위치가 없어도
    // 메시지 전송 자체는 막지 않는다 — 사용자가 특정 장소를 언급하면 서버가 현재 위치와
    // 무관하게 그 장소 기준으로 추천할 수 있기 때문이다.
    private var sessionLatitude: Double? = null
    private var sessionLongitude: Double? = null

    init {
        // "장소추천" 화면에 들어오자마자(이 ViewModel이 생성되는 시점에) 위치 수집 동의를
        // 구한다 — 메시지를 보내기 전에 이미 동의/위치 확보가 끝나 있어야 첫 메시지부터
        // 위치 기반 추천을 받을 수 있다.
        _showLocationConsentDialog.value = true
    }

    fun sendMessage(text: String) {
        if (text.isBlank() || _isSending.value) return

        _messages.update { it + ChatMessage.UserMessage(text) }
        _isSending.value = true

        // 위치 동의/조회가 아직 진행 중이라도(예: 사용자가 다이얼로그에 응답하기 전에 바로
        // 타이핑해 보낸 경우) 메시지 전송을 기다리게 하지 않는다. 그 시점까지 확보된
        // sessionLatitude/sessionLongitude(대부분 null)를 그대로 쓰고, 위치가 이후 확보되면
        // 다음 메시지부터 반영된다.
        dispatchMessage(text, sessionLatitude, sessionLongitude)
    }

    private fun hasLocationPermission(): Boolean {
        return ContextCompat.checkSelfPermission(
            appContext, Manifest.permission.ACCESS_FINE_LOCATION
        ) == PackageManager.PERMISSION_GRANTED ||
            ContextCompat.checkSelfPermission(
                appContext, Manifest.permission.ACCESS_COARSE_LOCATION
            ) == PackageManager.PERMISSION_GRANTED
    }

    // AiChatScreen이 "위치 정보를 수집합니다" 안내 다이얼로그의 결과를 받은 뒤 호출한다.
    // 동의해야만 (필요 시 시스템 권한 요청을 거쳐) 실제 GPS 조회로 이어진다.
    fun onLocationConsentResult(consented: Boolean) {
        _showLocationConsentDialog.value = false

        if (!consented) {
            sessionLatitude = null
            sessionLongitude = null
            return
        }

        if (hasLocationPermission()) {
            fetchLocation()
        } else {
            _locationPermissionRequests.tryEmit(Unit)
        }
    }

    // AiChatScreen이 시스템 권한 요청 다이얼로그 결과를 받은 뒤 호출한다.
    fun onLocationPermissionResult(granted: Boolean) {
        if (granted) {
            fetchLocation()
        } else {
            sessionLatitude = null
            sessionLongitude = null
        }
    }

    @SuppressLint("MissingPermission")
    private fun fetchLocation() {
        val fusedLocationClient = LocationServices.getFusedLocationProviderClient(appContext)

        fusedLocationClient.lastLocation
            .addOnSuccessListener { location ->
                sessionLatitude = location?.latitude
                sessionLongitude = location?.longitude
            }
            .addOnFailureListener {
                sessionLatitude = null
                sessionLongitude = null
            }
    }

    private fun dispatchMessage(text: String, latitude: Double?, longitude: Double?) {
        val existingSessionId = sessionId
        if (existingSessionId != null) {
            postMessage(existingSessionId, text, latitude, longitude)
        } else {
            createSessionThenSend(text, latitude, longitude)
        }
    }

    private fun createSessionThenSend(text: String, latitude: Double?, longitude: Double?) {
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
                        postMessage(body.sessionId, text, latitude, longitude)
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

    private fun postMessage(sessionId: Int, text: String, latitude: Double?, longitude: Double?) {
        RetrofitClient.instance
            .sendAiChatMessage(tripId, sessionId, AiChatRequest(text, latitude, longitude))
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
                                    suggestedEndTime = recommendation.suggestedEndTime,
                                    // 규칙 2: 서버가 이미 좌표를 확정한 추천만 내려주므로 그대로 신뢰해 사용한다.
                                    latitude = recommendation.latitude,
                                    longitude = recommendation.longitude
                                )
                            }

                            loadPlacePhoto(cardId, recommendation.placeName)
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

    // 사진만 별도 조회한다. 좌표는 서버가 이미 확정해 보냈으므로 여기서 덮어쓰지 않는다.
    private fun loadPlacePhoto(cardId: String, placeName: String) {
        placesRepository.fetchPlaceDetails(placeName) { details ->
            updateCard(cardId) {
                it.copy(
                    photoUrl = details.photoUrl,
                    photoLoading = false
                )
            }
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
            order = 0,
            latitude = card.latitude,
            longitude = card.longitude
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
    private val tripId: Int,
    private val appContext: Context
) : ViewModelProvider.Factory {
    override fun <T : ViewModel> create(modelClass: Class<T>): T {
        @Suppress("UNCHECKED_CAST")
        return AiChatViewModel(tripId, appContext) as T
    }
}
