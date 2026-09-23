package com.example.testbuild01.data.repository

import com.example.testbuild01.data.local.TicketType
import com.example.testbuild01.data.model.ScheduleCreateRequest
import com.example.testbuild01.data.model.ScheduleResponse
import com.example.testbuild01.data.model.Ticket
import com.example.testbuild01.data.model.TripResponse
import com.example.testbuild01.data.network.RemoteResult
import com.example.testbuild01.data.network.ScheduleRemote
import com.example.testbuild01.data.network.SyncError
import com.example.testbuild01.data.network.SyncErrorKind
import com.example.testbuild01.data.network.outcomeUnknown
import java.util.TimeZone

/** 여행 선택 목록의 한 항목. 숙박 기간이 여행 기간을 벗어나면 서버가 거부하므로 선택할 수 없다. */
data class TripChoice(val trip: TripResponse, val fitsStay: Boolean)

sealed interface LinkOutcome {
    data object Success : LinkOutcome
    /** 중복 일정 생성 방지: 이미 연결된 티켓은 해제 후에만 다시 반영할 수 있다. */
    data class AlreadyLinked(val tripId: Int, val scheduleId: Int) : LinkOutcome
    data object NotLinked : LinkOutcome
    /** 티켓이 없거나 호텔이 아니거나 상세 정보를 복호화하지 못한 경우 */
    data object NotAvailable : LinkOutcome
    data class Failed(val error: SyncError) : LinkOutcome
}

/**
 * 로컬 호텔 티켓과 서버 Schedule을 사용자의 명시적 선택에 따라서만 잇는다(자동 동기화 없음).
 *
 * 원칙: 서버 호출이 성공한 뒤에만 로컬을 파괴적으로 바꾼다. 서버 호출이 실패하면 로컬 티켓·연결 정보는 그대로 두고
 * 실패만 돌려준다. 호텔 정보 수정 자체는 서버와 무관하게 항상 로컬에 먼저 저장한다.
 */
class HotelScheduleLinker(
    private val repository: TicketRepository,
    private val remote: ScheduleRemote,
    private val timeZone: () -> TimeZone = { TimeZone.getDefault() }
) {
    suspend fun loadTripChoices(ticketId: Long): RemoteResult<List<TripChoice>> {
        val hotel = repository.getById(ticketId)?.hotel
        return when (val result = remote.getTrips()) {
            is RemoteResult.Success -> RemoteResult.Success(
                result.value.map { trip ->
                    TripChoice(trip, fitsStay = hotel != null && HotelScheduleMapper.fitsTrip(hotel, trip, timeZone()))
                }
            )
            is RemoteResult.Failure -> result
        }
    }

    suspend fun loadTrips(): RemoteResult<List<TripResponse>> = remote.getTrips()

    /**
     * 일정 생성 후 연결. 응답 유실(타임아웃 등)로 "서버엔 생성됐지만 앱은 실패로 아는" 경우 재시도하면 중복이 생기므로,
     * 만들기 전과 결과가 불확실한 실패 직후에 같은 호텔 일정이 이미 있는지 찾아 있으면 그 일정에 연결한다.
     * 재설치로 로컬 연결 정보를 잃은 뒤 다시 반영하는 경우도 같은 방식으로 중복을 막는다.
     */
    suspend fun link(ticketId: Long, tripId: Int): LinkOutcome {
        val ticket = repository.getById(ticketId) ?: return LinkOutcome.NotAvailable
        ticket.alreadyLinked()?.let { return it }
        val hotel = ticket.hotel?.takeIf { ticket.type == TicketType.HOTEL } ?: return LinkOutcome.NotAvailable
        val request = HotelScheduleMapper.toCreateRequest(hotel, timeZone())

        when (val existing = findSameSchedule(tripId, request)) {
            is RemoteResult.Success -> existing.value?.let { return adopt(ticketId, tripId, it) }
            is RemoteResult.Failure -> return LinkOutcome.Failed(existing.error)
        }

        return when (val result = remote.createSchedule(tripId, request)) {
            is RemoteResult.Success -> adopt(ticketId, tripId, result.value)
            is RemoteResult.Failure -> {
                if (result.error.outcomeUnknown) {
                    val recovered = (findSameSchedule(tripId, request) as? RemoteResult.Success)?.value
                    if (recovered != null) return adopt(ticketId, tripId, recovered)
                }
                LinkOutcome.Failed(result.error)
            }
        }
    }

    private suspend fun adopt(ticketId: Long, tripId: Int, schedule: ScheduleResponse): LinkOutcome {
        repository.setScheduleLink(ticketId, scheduleId = schedule.id, tripId = tripId)
        return LinkOutcome.Success
    }

    /** 같은 여행에 이름·체크인·체크아웃이 모두 같은 호텔 체크인 일정. 없으면 Success(null). */
    private suspend fun findSameSchedule(tripId: Int, request: ScheduleCreateRequest): RemoteResult<ScheduleResponse?> =
        when (val result = remote.getSchedules(tripId)) {
            is RemoteResult.Success -> RemoteResult.Success(
                result.value.firstOrNull { HotelScheduleMapper.isSameHotelSchedule(it, request) }
            )
            is RemoteResult.Failure -> result
        }

    /**
     * 호텔 정보 수정. 서버와 무관하게 로컬 저장을 먼저 끝낸다(서버 실패로 수정 내용이 유실되지 않도록).
     * 연결된 일정에 반영되는 값(이름·주소·좌표·시각)이 바뀌었으면 true를 돌려준다. 이때 화면은 사용자에게 물어
     * 수락하면 [syncLinkedSchedule]을 호출하고, 거부하면 아무것도 하지 않는다(서버 일정은 로컬과 달라질 수 있다).
     */
    suspend fun updateHotel(ticketId: Long, draft: TicketDraft): Boolean {
        val before = repository.getById(ticketId)
        repository.update(ticketId, draft)
        return before != null &&
            before.isLinkedToSchedule &&
            draft.type == TicketType.HOTEL &&
            HotelScheduleMapper.scheduleFieldsChanged(before.hotel, draft.hotel)
    }

    /** 현재 로컬 호텔 값으로 연결된 일정을 갱신한다. 서버 PUT이 전체 덮어쓰기라 먼저 현재 값을 조회한다. */
    suspend fun syncLinkedSchedule(ticketId: Long): LinkOutcome {
        val ticket = repository.getById(ticketId) ?: return LinkOutcome.NotAvailable
        val tripId = ticket.linkedTripId
        val scheduleId = ticket.linkedScheduleId
        if (tripId == null || scheduleId == null) return LinkOutcome.NotLinked
        val hotel = ticket.hotel ?: return LinkOutcome.NotAvailable

        val current = when (val result = remote.getSchedule(tripId, scheduleId)) {
            is RemoteResult.Success -> result.value
            is RemoteResult.Failure -> return LinkOutcome.Failed(result.error)
        }
        val request = HotelScheduleMapper.toUpdateRequest(current, hotel, timeZone())
        return when (val result = remote.updateSchedule(tripId, scheduleId, request)) {
            is RemoteResult.Success -> LinkOutcome.Success
            is RemoteResult.Failure -> LinkOutcome.Failed(result.error)
        }
    }

    /**
     * 연결 해제. [deleteSchedule]이 true면 서버 일정을 먼저 지우고, 성공(또는 이미 없음)했을 때만 로컬 연결을 푼다.
     * false면 서버 일정은 남겨 두고 로컬 연결만 푼다.
     */
    suspend fun unlink(ticketId: Long, deleteSchedule: Boolean): LinkOutcome {
        val ticket = repository.getById(ticketId) ?: return LinkOutcome.NotAvailable
        if (!ticket.isLinkedToSchedule) return LinkOutcome.NotLinked
        if (deleteSchedule) deleteRemoteSchedule(ticket)?.let { return it }
        repository.clearScheduleLink(ticketId)
        return LinkOutcome.Success
    }

    /**
     * 티켓 삭제. [deleteSchedule]이 true면 서버 일정을 먼저 지우고, 실패하면 티켓도 지우지 않는다
     * (사용자가 다시 "티켓만 삭제"를 고를 수 있게). 연결이 없으면 [deleteSchedule]은 무시된다.
     */
    suspend fun deleteTicket(ticket: Ticket, deleteSchedule: Boolean): LinkOutcome {
        val current = repository.getById(ticket.id) ?: return LinkOutcome.NotAvailable
        if (deleteSchedule && current.isLinkedToSchedule) deleteRemoteSchedule(current)?.let { return it }
        repository.delete(current)
        return LinkOutcome.Success
    }

    /** 성공 시 null. 다른 멤버가 이미 지운 일정(404)은 목적을 달성한 것으로 보고 성공 처리한다. */
    private suspend fun deleteRemoteSchedule(ticket: Ticket): LinkOutcome? {
        val result = remote.deleteSchedule(ticket.linkedTripId!!, ticket.linkedScheduleId!!)
        if (result is RemoteResult.Failure && result.error.kind != SyncErrorKind.NOT_FOUND) {
            return LinkOutcome.Failed(result.error)
        }
        return null
    }

    private fun Ticket.alreadyLinked(): LinkOutcome.AlreadyLinked? {
        val tripId = linkedTripId ?: return null
        val scheduleId = linkedScheduleId ?: return null
        return LinkOutcome.AlreadyLinked(tripId, scheduleId)
    }
}
