package com.example.testbuild01.data.network

import com.example.testbuild01.data.model.ScheduleCreateRequest
import com.example.testbuild01.data.model.ScheduleResponse
import com.example.testbuild01.data.model.ScheduleUpdateRequest
import com.example.testbuild01.data.model.TripResponse
import com.google.gson.stream.MalformedJsonException
import kotlinx.coroutines.CancellationException
import retrofit2.Response
import java.io.IOException

/**
 * 서버 호출 실패 종류. 화면은 이 값에 맞는 안내 문구만 고른다.
 * INVALID_RESPONSE는 서버가 성공 응답을 보냈지만 본문을 해석하지 못한 경우(모델 불일치 등)다.
 */
enum class SyncErrorKind { NETWORK, UNAUTHORIZED, FORBIDDEN, NOT_FOUND, REJECTED, SERVER, INVALID_RESPONSE }

/**
 * 서버에 요청이 반영됐는지 알 수 없는 실패. 응답이 유실됐거나(NETWORK), 저장 후 오류가 났거나(SERVER),
 * 성공 응답을 해석하지 못한(INVALID_RESPONSE) 경우라 "생성 안 됨"으로 단정하면 중복이 생길 수 있다.
 */
val SyncError.outcomeUnknown: Boolean
    get() = kind == SyncErrorKind.NETWORK || kind == SyncErrorKind.SERVER || kind == SyncErrorKind.INVALID_RESPONSE

/**
 * 호출 중 던져진 예외 분류. 연결 실패·타임아웃(IOException)만 네트워크 오류로 보고,
 * Gson 변환 실패처럼 서버는 응답했지만 앱이 해석하지 못한 경우는 INVALID_RESPONSE로 구분한다.
 * MalformedJsonException은 IOException의 하위 타입이지만 본문 형식 오류라 먼저 걸러낸다.
 */
internal fun errorKindOf(e: Exception): SyncErrorKind = when (e) {
    is MalformedJsonException -> SyncErrorKind.INVALID_RESPONSE
    is IOException -> SyncErrorKind.NETWORK
    else -> SyncErrorKind.INVALID_RESPONSE
}

/** [serverMessage]는 400처럼 서버가 사람이 읽을 사유를 돌려준 경우에만 채운다. */
data class SyncError(val kind: SyncErrorKind, val serverMessage: String? = null)

sealed interface RemoteResult<out T> {
    data class Success<T>(val value: T) : RemoteResult<T>
    data class Failure(val error: SyncError) : RemoteResult<Nothing>
}

/** 호텔 티켓 ↔ 일정 연동에 필요한 서버 호출만 모은 경계. 테스트에서는 가짜 구현으로 바꾼다. */
interface ScheduleRemote {
    suspend fun getTrips(): RemoteResult<List<TripResponse>>
    suspend fun getSchedules(tripId: Int): RemoteResult<List<ScheduleResponse>>
    suspend fun getSchedule(tripId: Int, scheduleId: Int): RemoteResult<ScheduleResponse>
    suspend fun createSchedule(tripId: Int, request: ScheduleCreateRequest): RemoteResult<ScheduleResponse>
    suspend fun updateSchedule(tripId: Int, scheduleId: Int, request: ScheduleUpdateRequest): RemoteResult<ScheduleResponse>
    suspend fun deleteSchedule(tripId: Int, scheduleId: Int): RemoteResult<Unit>
}

/** RetrofitClient는 Application.onCreate에서 초기화되므로 호출 시점에 인스턴스를 가져온다. */
class RetrofitScheduleRemote(
    private val api: () -> TravelApiService = { RetrofitClient.instance }
) : ScheduleRemote {

    override suspend fun getTrips() = executeWithBody { api().getTripsSuspend() }

    override suspend fun getSchedules(tripId: Int) = executeWithBody { api().getSchedulesSuspend(tripId) }

    override suspend fun getSchedule(tripId: Int, scheduleId: Int) =
        executeWithBody { api().getScheduleSuspend(tripId, scheduleId) }

    override suspend fun createSchedule(tripId: Int, request: ScheduleCreateRequest) =
        executeWithBody { api().createScheduleSuspend(tripId, request) }

    override suspend fun updateSchedule(tripId: Int, scheduleId: Int, request: ScheduleUpdateRequest) =
        executeWithBody { api().updateScheduleSuspend(tripId, scheduleId, request) }

    override suspend fun deleteSchedule(tripId: Int, scheduleId: Int): RemoteResult<Unit> =
        when (val result = execute { api().deleteScheduleSuspend(tripId, scheduleId) }) {
            is RemoteResult.Success -> RemoteResult.Success(Unit)
            is RemoteResult.Failure -> result
        }

    private suspend fun <T : Any> executeWithBody(block: suspend () -> Response<T>): RemoteResult<T> =
        when (val result = execute(block)) {
            is RemoteResult.Success ->
                result.value.body()?.let { RemoteResult.Success(it) }
                    ?: RemoteResult.Failure(SyncError(SyncErrorKind.INVALID_RESPONSE))
            is RemoteResult.Failure -> result
        }

    private suspend fun <T> execute(block: suspend () -> Response<T>): RemoteResult<Response<T>> {
        val response = try {
            block()
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            return RemoteResult.Failure(SyncError(errorKindOf(e)))
        }
        if (response.isSuccessful) return RemoteResult.Success(response)
        val kind = when (response.code()) {
            401 -> SyncErrorKind.UNAUTHORIZED
            403 -> SyncErrorKind.FORBIDDEN
            404 -> SyncErrorKind.NOT_FOUND
            in 400..499 -> SyncErrorKind.REJECTED
            else -> SyncErrorKind.SERVER
        }
        return RemoteResult.Failure(SyncError(kind, readableMessage(response)))
    }

    /** 컨트롤러가 BadRequest("...")로 돌려준 평문만 쓰고, 검증 실패 JSON(ProblemDetails)은 버린다. */
    private fun readableMessage(response: Response<*>): String? {
        val raw = runCatching { response.errorBody()?.string() }.getOrNull()?.trim()
        if (raw.isNullOrEmpty() || raw.startsWith("{") || raw.startsWith("<")) return null
        return raw.removeSurrounding("\"").take(200)
    }
}
