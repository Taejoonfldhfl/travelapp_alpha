package com.example.testbuild01.data.repository

import com.example.testbuild01.data.local.HotelDetail
import com.example.testbuild01.data.local.TicketType
import com.example.testbuild01.data.model.ScheduleCreateRequest
import com.example.testbuild01.data.model.ScheduleResponse
import com.example.testbuild01.data.model.ScheduleUpdateRequest
import com.example.testbuild01.data.model.TripResponse
import com.example.testbuild01.data.network.RemoteResult
import com.example.testbuild01.data.network.ScheduleRemote
import com.example.testbuild01.data.network.SyncError
import com.example.testbuild01.data.network.SyncErrorKind
import com.example.testbuild01.notification.AlarmBackend
import com.example.testbuild01.notification.AlarmSlot
import com.example.testbuild01.notification.TicketAlarmScheduler
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.TimeZone

/** 호텔 티켓 ↔ 일정 연동: 사용자의 선택에 따른 서버 호출 여부와, 서버 실패 시 로컬 보존을 확인한다. */
class HotelScheduleLinkerTest {

    private class NoopBackend : AlarmBackend {
        override fun canScheduleExact() = true
        override fun scheduleExact(ticketId: Long, slot: AlarmSlot, triggerAtMillis: Long) = Unit
        override fun scheduleFallback(ticketId: Long, slot: AlarmSlot, triggerAtMillis: Long) = Unit
        override fun cancel(ticketId: Long, slot: AlarmSlot) = Unit
    }

    /** 서버 일정 저장소를 흉내 낸다. [failWith]를 채우면 모든 호출이 그 오류로 실패한다. */
    private class FakeScheduleRemote : ScheduleRemote {
        val schedules = mutableMapOf<Int, ScheduleResponse>()
        val created = mutableListOf<Pair<Int, ScheduleCreateRequest>>()
        val updated = mutableListOf<ScheduleUpdateRequest>()
        val deleted = mutableListOf<Int>()
        var trips = listOf(trip(id = 7, start = "2026-10-01", end = "2026-10-05"))
        var failWith: SyncError? = null
        /** true면 서버에는 일정이 생성되지만 응답이 유실된 것처럼 [lostResponseError]로 실패를 돌려준다. */
        var loseCreateResponse = false
        var lostResponseError = SyncError(SyncErrorKind.NETWORK)
        /** true면 일정 목록 조회만 네트워크 오류로 실패한다. */
        var failScheduleListing = false
        /** true면 일정이 하나라도 생성된 뒤의 목록 조회만 실패한다(생성 직후 확인 조회 실패 재현). */
        var failListingAfterCreate = false
        private var nextId = 100

        override suspend fun getTrips(): RemoteResult<List<TripResponse>> = fail() ?: RemoteResult.Success(trips)

        override suspend fun getSchedules(tripId: Int): RemoteResult<List<ScheduleResponse>> {
            fail()?.let { return it }
            if (failScheduleListing || (failListingAfterCreate && created.isNotEmpty())) return RemoteResult.Failure(SyncError(SyncErrorKind.NETWORK))
            return RemoteResult.Success(schedules.values.filter { it.tripId == tripId })
        }

        override suspend fun getSchedule(tripId: Int, scheduleId: Int): RemoteResult<ScheduleResponse> =
            fail() ?: schedules[scheduleId]?.let { RemoteResult.Success(it) } ?: notFound()

        override suspend fun createSchedule(tripId: Int, request: ScheduleCreateRequest): RemoteResult<ScheduleResponse> {
            fail()?.let { return it }
            created += tripId to request
            val schedule = ScheduleResponse(
                id = nextId++, tripId = tripId, title = request.title, placeName = request.placeName,
                description = request.description, startTime = request.startTime, endTime = request.endTime,
                order = request.order, latitude = request.latitude, longitude = request.longitude,
                priority = 0, isEssential = true, isHotelCheckIn = request.isHotelCheckIn, createdAt = ""
            )
            schedules[schedule.id] = schedule
            if (loseCreateResponse) return RemoteResult.Failure(lostResponseError)
            return RemoteResult.Success(schedule)
        }

        override suspend fun updateSchedule(tripId: Int, scheduleId: Int, request: ScheduleUpdateRequest): RemoteResult<ScheduleResponse> {
            fail()?.let { return it }
            val current = schedules[scheduleId] ?: return notFound()
            updated += request
            val next = current.copy(
                title = request.title, placeName = request.placeName, description = request.description,
                startTime = request.startTime, endTime = request.endTime, order = request.order,
                latitude = request.latitude, longitude = request.longitude, priority = request.priority,
                isEssential = request.isEssential, isHotelCheckIn = request.isHotelCheckIn
            )
            schedules[scheduleId] = next
            return RemoteResult.Success(next)
        }

        override suspend fun deleteSchedule(tripId: Int, scheduleId: Int): RemoteResult<Unit> {
            fail()?.let { return it }
            if (schedules.remove(scheduleId) == null) return notFound()
            deleted += scheduleId
            return RemoteResult.Success(Unit)
        }

        val callCount get() = created.size + updated.size + deleted.size

        private fun fail(): RemoteResult.Failure? = failWith?.let { RemoteResult.Failure(it) }
        private fun notFound() = RemoteResult.Failure(SyncError(SyncErrorKind.NOT_FOUND))
    }

    private val utc = TimeZone.getTimeZone("UTC")
    private val dao = FakeTicketDao()
    private val repository = TicketRepository(
        dao = dao,
        cipher = ReversingCipher(),
        scheduler = TicketAlarmScheduler(NoopBackend(), now = { 0L }),
        now = { 0L }
    )
    private val remote = FakeScheduleRemote()
    private val linker = HotelScheduleLinker(repository, remote, timeZone = { utc })

    // 2026-10-02 15:00 UTC 체크인, 2026-10-04 11:00 UTC 체크아웃
    private val checkIn = 1_790_953_200_000L
    private val checkOut = 1_791_111_600_000L

    private fun hotel(checkInTime: Long = checkIn, checkOutTime: Long = checkOut) = HotelDetail(
        hotelName = "그랜드 호텔",
        address = "부산 해운대구 1",
        latitude = 35.16,
        longitude = 129.16,
        checkInTime = checkInTime,
        checkOutTime = checkOutTime,
        roomType = "디럭스",
        guestCount = 2,
        confirmationNumber = "CONF-SECRET-777",
        guestNameOnBooking = "HONG GILDONG",
        freeCancellationDeadline = null,
        phoneNumber = null
    )

    private fun draft(detail: HotelDetail = hotel()) = TicketDraft(
        type = TicketType.HOTEL,
        title = detail.hotelName,
        startDateTime = detail.checkInTime,
        locationFrom = detail.address,
        locationTo = "",
        confirmationNumber = detail.confirmationNumber,
        hotel = detail
    )

    private suspend fun addLinkedHotel(): Long {
        val id = repository.add(draft())
        assertEquals(LinkOutcome.Success, linker.link(id, tripId = 7))
        return id
    }

    private suspend fun ticket(id: Long) = repository.getById(id)

    // ---- 반영(생성) ----

    @Test
    fun link_createsHotelCheckInScheduleAndStoresIds() = runTest {
        val id = repository.add(draft())

        val outcome = linker.link(id, tripId = 7)

        assertEquals(LinkOutcome.Success, outcome)
        val (tripId, request) = remote.created.single()
        assertEquals(7, tripId)
        assertEquals("그랜드 호텔", request.title)
        assertEquals("부산 해운대구 1", request.placeName)
        assertEquals("2026-10-02T15:00:00", request.startTime)
        assertEquals("2026-10-04T11:00:00", request.endTime)
        assertEquals(35.16, request.latitude!!, 0.0)
        assertTrue(request.isHotelCheckIn)
        val saved = ticket(id)!!
        assertEquals(7, saved.linkedTripId)
        assertEquals(remote.schedules.keys.single(), saved.linkedScheduleId)
    }

    @Test
    fun link_whenAlreadyLinked_doesNotCreateDuplicate() = runTest {
        val id = addLinkedHotel()
        val scheduleId = ticket(id)!!.linkedScheduleId!!

        val outcome = linker.link(id, tripId = 7)

        assertEquals(LinkOutcome.AlreadyLinked(tripId = 7, scheduleId = scheduleId), outcome)
        assertEquals(1, remote.created.size)
    }

    @Test
    fun link_afterUnlink_canLinkAgain() = runTest {
        val id = addLinkedHotel()
        linker.unlink(id, deleteSchedule = true)

        assertEquals(LinkOutcome.Success, linker.link(id, tripId = 7))
        assertEquals(2, remote.created.size)
    }

    @Test
    fun link_responseLost_adoptsScheduleServerCreated() = runTest {
        val id = repository.add(draft())
        remote.loseCreateResponse = true

        val outcome = linker.link(id, tripId = 7)

        assertEquals(LinkOutcome.Success, outcome)
        assertEquals(1, remote.schedules.size)
        assertEquals(remote.schedules.keys.single(), ticket(id)!!.linkedScheduleId)
    }

    @Test
    fun link_unparseableSuccessResponse_adoptsScheduleServerCreated() = runTest {
        val id = repository.add(draft())
        remote.loseCreateResponse = true
        remote.lostResponseError = SyncError(SyncErrorKind.INVALID_RESPONSE)

        assertEquals(LinkOutcome.Success, linker.link(id, tripId = 7))
        assertEquals(1, remote.schedules.size)
    }

    @Test
    fun link_retryAfterUnrecoverableLoss_doesNotCreateDuplicate() = runTest {
        val id = repository.add(draft())
        // 생성 응답도, 직후 확인 조회도 실패 → 이번엔 실패로 끝난다(서버엔 생성됨).
        remote.loseCreateResponse = true
        remote.failListingAfterCreate = true
        assertTrue(linker.link(id, tripId = 7) is LinkOutcome.Failed)
        assertFalse(ticket(id)!!.isLinkedToSchedule)

        // 네트워크가 돌아온 뒤 사용자가 다시 반영하면 새로 만들지 않고 기존 일정에 연결한다.
        remote.loseCreateResponse = false
        remote.failListingAfterCreate = false
        assertEquals(LinkOutcome.Success, linker.link(id, tripId = 7))

        assertEquals(1, remote.created.size)
        assertEquals(1, remote.schedules.size)
        assertTrue(ticket(id)!!.isLinkedToSchedule)
    }

    @Test
    fun link_whenSameHotelScheduleAlreadyOnServer_adoptsIt() = runTest {
        // 재설치 등으로 로컬 연결 정보만 사라진 상황
        val first = addLinkedHotel()
        repository.clearScheduleLink(first)

        assertEquals(LinkOutcome.Success, linker.link(first, tripId = 7))
        assertEquals(1, remote.created.size)
    }

    @Test
    fun link_differentTimesOnServer_createsNewSchedule() = runTest {
        val first = addLinkedHotel()
        repository.clearScheduleLink(first)
        repository.update(first, draft(hotel(checkIn + 3_600_000L)))

        assertEquals(LinkOutcome.Success, linker.link(first, tripId = 7))
        assertEquals(2, remote.created.size)
    }

    @Test
    fun link_whenScheduleListingFails_doesNotCreate() = runTest {
        val id = repository.add(draft())
        remote.failScheduleListing = true

        assertEquals(LinkOutcome.Failed(SyncError(SyncErrorKind.NETWORK)), linker.link(id, tripId = 7))
        assertTrue(remote.created.isEmpty())
    }

    @Test
    fun loadTripChoices_marksTripsNotCoveringStay() = runTest {
        remote.trips = listOf(
            trip(id = 7, start = "2026-10-01", end = "2026-10-05"),
            trip(id = 8, start = "2026-10-03", end = "2026-10-05"),
            trip(id = 9, start = "2026-10-01", end = "2026-10-03")
        )
        val id = repository.add(draft())

        val choices = (linker.loadTripChoices(id) as RemoteResult.Success).value

        assertEquals(listOf(true, false, false), choices.map { it.fitsStay })
    }

    @Test
    fun loadTripChoices_emptyTrips_returnsEmptyList() = runTest {
        remote.trips = emptyList()
        val id = repository.add(draft())

        assertEquals(emptyList<TripChoice>(), (linker.loadTripChoices(id) as RemoteResult.Success).value)
    }

    // ---- 수정: 서버 갱신 수락/거부 ----

    // 화면(TicketViewModel)과 같은 순서: updateHotel로 로컬 저장 + 질문 여부 판단 → 수락 시 syncLinkedSchedule

    @Test
    fun updateHotel_accepted_updatesLocalAndServerTimes() = runTest {
        val id = addLinkedHotel()
        val hour = 3_600_000L

        val shouldAsk = linker.updateHotel(id, draft(hotel(checkIn + hour, checkOut + hour)))
        val outcome = linker.syncLinkedSchedule(id)

        assertTrue(shouldAsk)
        assertEquals(LinkOutcome.Success, outcome)
        assertEquals(checkIn + hour, ticket(id)!!.hotel!!.checkInTime)
        val request = remote.updated.single()
        assertEquals("2026-10-02T16:00:00", request.startTime)
        assertEquals("2026-10-04T12:00:00", request.endTime)
        // PUT은 전체 덮어쓰기이므로 좌표와 체크인 앵커 플래그가 초기화되면 안 된다.
        assertEquals(35.16, request.latitude!!, 0.0)
        assertTrue(request.isHotelCheckIn)
    }

    @Test
    fun updateHotel_declined_updatesLocalOnlyAndKeepsLink() = runTest {
        val id = addLinkedHotel()
        val serverBefore = remote.schedules.values.single()
        val hour = 3_600_000L

        val shouldAsk = linker.updateHotel(id, draft(hotel(checkIn + hour)))
        // 사용자가 "티켓만 수정"을 고르면 추가 호출이 없다.

        assertTrue(shouldAsk)
        assertTrue(remote.updated.isEmpty())
        assertEquals(serverBefore, remote.schedules.values.single())
        val local = ticket(id)!!
        assertEquals(checkIn + hour, local.hotel!!.checkInTime)
        assertTrue(local.isLinkedToSchedule)
    }

    @Test
    fun updateHotel_privateFieldsOnly_doesNotAsk() = runTest {
        val id = addLinkedHotel()

        assertFalse(linker.updateHotel(id, draft(hotel().copy(confirmationNumber = "NEW-1", guestCount = 3))))
        assertEquals("NEW-1", ticket(id)!!.confirmationNumber)
    }

    @Test
    fun updateHotel_notLinked_doesNotAsk() = runTest {
        val id = repository.add(draft())

        assertFalse(linker.updateHotel(id, draft(hotel(checkIn + 3_600_000L))))
        assertEquals(0, remote.callCount)
    }

    @Test
    fun syncLinkedSchedule_preservesFieldsOtherMembersEdited() = runTest {
        val id = addLinkedHotel()
        val scheduleId = ticket(id)!!.linkedScheduleId!!
        remote.schedules[scheduleId] = remote.schedules.getValue(scheduleId).copy(description = "공항에서 바로 이동", order = 3)

        linker.syncLinkedSchedule(id)

        val request = remote.updated.single()
        assertEquals("공항에서 바로 이동", request.description)
        assertEquals(3, request.order)
    }

    @Test
    fun syncLinkedSchedule_whenNotLinked_makesNoServerCall() = runTest {
        val id = repository.add(draft())

        assertEquals(LinkOutcome.NotLinked, linker.syncLinkedSchedule(id))
        assertEquals(0, remote.callCount)
    }

    // ---- 삭제 ----

    @Test
    fun deleteTicket_ticketOnly_keepsServerSchedule() = runTest {
        val id = addLinkedHotel()

        val outcome = linker.deleteTicket(ticket(id)!!, deleteSchedule = false)

        assertEquals(LinkOutcome.Success, outcome)
        assertNull(ticket(id))
        assertTrue(remote.deleted.isEmpty())
        assertEquals(1, remote.schedules.size)
    }

    @Test
    fun deleteTicket_withSchedule_deletesBoth() = runTest {
        val id = addLinkedHotel()
        val scheduleId = ticket(id)!!.linkedScheduleId!!

        val outcome = linker.deleteTicket(ticket(id)!!, deleteSchedule = true)

        assertEquals(LinkOutcome.Success, outcome)
        assertNull(ticket(id))
        assertEquals(listOf(scheduleId), remote.deleted)
    }

    @Test
    fun deleteTicket_withSchedule_alreadyDeletedOnServer_stillDeletesTicket() = runTest {
        val id = addLinkedHotel()
        remote.schedules.clear() // 다른 멤버가 먼저 지운 상황

        assertEquals(LinkOutcome.Success, linker.deleteTicket(ticket(id)!!, deleteSchedule = true))
        assertNull(ticket(id))
    }

    @Test
    fun unlink_withoutDelete_clearsLinkAndKeepsSchedule() = runTest {
        val id = addLinkedHotel()

        assertEquals(LinkOutcome.Success, linker.unlink(id, deleteSchedule = false))
        assertFalse(ticket(id)!!.isLinkedToSchedule)
        assertEquals(1, remote.schedules.size)
    }

    // ---- 서버 실패 시 로컬 보존 ----

    @Test
    fun link_serverFailure_leavesTicketUnlinked() = runTest {
        val id = repository.add(draft())
        remote.failWith = SyncError(SyncErrorKind.NETWORK)

        val outcome = linker.link(id, tripId = 7)

        assertEquals(LinkOutcome.Failed(SyncError(SyncErrorKind.NETWORK)), outcome)
        val local = ticket(id)!!
        assertFalse(local.isLinkedToSchedule)
        assertEquals("CONF-SECRET-777", local.confirmationNumber)
    }

    @Test
    fun updateHotel_serverFailure_keepsLocalEditAndLink() = runTest {
        val id = addLinkedHotel()
        remote.failWith = SyncError(SyncErrorKind.FORBIDDEN)
        val hour = 3_600_000L

        linker.updateHotel(id, draft(hotel(checkIn + hour)))
        val outcome = linker.syncLinkedSchedule(id)

        assertEquals(LinkOutcome.Failed(SyncError(SyncErrorKind.FORBIDDEN)), outcome)
        val local = ticket(id)!!
        assertEquals(checkIn + hour, local.hotel!!.checkInTime)
        assertTrue(local.isLinkedToSchedule)
    }

    @Test
    fun deleteTicket_serverFailure_keepsTicketAndLink() = runTest {
        val id = addLinkedHotel()
        remote.failWith = SyncError(SyncErrorKind.SERVER)

        val outcome = linker.deleteTicket(ticket(id)!!, deleteSchedule = true)

        assertEquals(LinkOutcome.Failed(SyncError(SyncErrorKind.SERVER)), outcome)
        val local = ticket(id)
        assertNotNull(local)
        assertTrue(local!!.isLinkedToSchedule)
        assertEquals("CONF-SECRET-777", local.confirmationNumber)
    }

    @Test
    fun unlink_withDelete_serverFailure_keepsLink() = runTest {
        val id = addLinkedHotel()
        remote.failWith = SyncError(SyncErrorKind.NETWORK)

        assertTrue(linker.unlink(id, deleteSchedule = true) is LinkOutcome.Failed)
        assertTrue(ticket(id)!!.isLinkedToSchedule)
    }

    private companion object {
        fun trip(id: Int, start: String, end: String) =
            TripResponse(id = id, title = "여행$id", startDate = "${start}T00:00:00", endDate = "${end}T00:00:00", createdAt = "")
    }
}
