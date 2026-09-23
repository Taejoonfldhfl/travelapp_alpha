package com.example.testbuild01.ui.hotel

import android.content.Context
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.example.testbuild01.data.local.HotelDetail
import com.example.testbuild01.data.local.TicketCipher
import com.example.testbuild01.data.local.TicketDatabase
import com.example.testbuild01.data.local.TicketType
import com.example.testbuild01.data.model.ScheduleCreateRequest
import com.example.testbuild01.data.model.ScheduleResponse
import com.example.testbuild01.data.model.ScheduleUpdateRequest
import com.example.testbuild01.data.model.Ticket
import com.example.testbuild01.data.model.TripResponse
import com.example.testbuild01.data.network.RemoteResult
import com.example.testbuild01.data.network.ScheduleRemote
import com.example.testbuild01.data.network.SyncError
import com.example.testbuild01.data.network.SyncErrorKind
import com.example.testbuild01.data.repository.HotelScheduleLinker
import com.example.testbuild01.data.repository.LinkOutcome
import com.example.testbuild01.data.repository.TicketDraft
import com.example.testbuild01.data.repository.TicketRepository
import com.example.testbuild01.notification.AlarmBackend
import com.example.testbuild01.notification.AlarmSlot
import com.example.testbuild01.notification.TicketAlarmScheduler
import com.example.testbuild01.ui.ticket.TicketListScreen
import com.example.testbuild01.ui.ticket.TicketViewModel
import com.google.android.gms.maps.model.LatLng
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import java.util.TimeZone

/**
 * 실제 티켓 목록 화면 + 다이얼로그 + ViewModel + linker + (메모리) Room을 그대로 쓰고,
 * 서버만 가짜로 바꿔 호텔 ↔ 여행 일정 연동 다이얼로그 흐름을 기기에서 조작해 확인한다.
 */
@RunWith(AndroidJUnit4::class)
class HotelScheduleLinkDialogTest {

    @get:Rule
    val rule = createComposeRule()

    private class NoopBackend : AlarmBackend {
        override fun canScheduleExact() = true
        override fun scheduleExact(ticketId: Long, slot: AlarmSlot, triggerAtMillis: Long) = Unit
        override fun scheduleFallback(ticketId: Long, slot: AlarmSlot, triggerAtMillis: Long) = Unit
        override fun cancel(ticketId: Long, slot: AlarmSlot) = Unit
    }

    /** 키스토어 없이 돌도록 한 가역 변환. 암호화 자체는 이 테스트의 관심사가 아니다. */
    private class ReversingCipher : TicketCipher {
        override fun encrypt(plainText: String) = "enc:" + plainText.reversed()
        override fun decrypt(cipherText: String) = cipherText.removePrefix("enc:").reversed()
    }

    private class FakeScheduleRemote : ScheduleRemote {
        val schedules = mutableMapOf<Int, ScheduleResponse>()
        val created = mutableListOf<ScheduleCreateRequest>()
        val updated = mutableListOf<ScheduleUpdateRequest>()
        val deleted = mutableListOf<Int>()
        @Volatile var trips: List<TripResponse> = emptyList()
        /** 채우면 생성·수정·삭제만 이 오류로 실패한다(여행/일정 조회는 성공). */
        @Volatile var failWritesWith: SyncError? = null
        private var nextId = 100

        override suspend fun getTrips(): RemoteResult<List<TripResponse>> = RemoteResult.Success(trips)

        override suspend fun getSchedules(tripId: Int): RemoteResult<List<ScheduleResponse>> =
            RemoteResult.Success(synchronized(this) { schedules.values.filter { it.tripId == tripId } })

        override suspend fun getSchedule(tripId: Int, scheduleId: Int): RemoteResult<ScheduleResponse> =
            synchronized(this) { schedules[scheduleId] }?.let { RemoteResult.Success(it) }
                ?: RemoteResult.Failure(SyncError(SyncErrorKind.NOT_FOUND))

        override suspend fun createSchedule(tripId: Int, request: ScheduleCreateRequest): RemoteResult<ScheduleResponse> {
            failWritesWith?.let { return RemoteResult.Failure(it) }
            return synchronized(this) {
                created += request
                val schedule = ScheduleResponse(
                    id = nextId++, tripId = tripId, title = request.title, placeName = request.placeName,
                    description = request.description, startTime = request.startTime, endTime = request.endTime,
                    order = request.order, latitude = request.latitude, longitude = request.longitude,
                    priority = 0, isEssential = true, isHotelCheckIn = request.isHotelCheckIn, createdAt = ""
                )
                schedules[schedule.id] = schedule
                RemoteResult.Success(schedule)
            }
        }

        override suspend fun updateSchedule(tripId: Int, scheduleId: Int, request: ScheduleUpdateRequest): RemoteResult<ScheduleResponse> {
            failWritesWith?.let { return RemoteResult.Failure(it) }
            return synchronized(this) {
                val current = schedules[scheduleId] ?: return RemoteResult.Failure(SyncError(SyncErrorKind.NOT_FOUND))
                updated += request
                val next = current.copy(startTime = request.startTime, endTime = request.endTime, title = request.title)
                schedules[scheduleId] = next
                RemoteResult.Success(next)
            }
        }

        override suspend fun deleteSchedule(tripId: Int, scheduleId: Int): RemoteResult<Unit> {
            failWritesWith?.let { return RemoteResult.Failure(it) }
            return synchronized(this) {
                if (schedules.remove(scheduleId) == null) return RemoteResult.Failure(SyncError(SyncErrorKind.NOT_FOUND))
                deleted += scheduleId
                RemoteResult.Success(Unit)
            }
        }
    }

    private val utc = TimeZone.getTimeZone("UTC")
    // 2026-10-02 15:00 UTC 체크인, 2026-10-04 11:00 UTC 체크아웃
    private val checkIn = 1_790_953_200_000L
    private val checkOut = 1_791_111_600_000L
    private val hour = 3_600_000L

    private val busanTrip = trip(id = 7, title = "부산 여행", start = "2026-10-01", end = "2026-10-05")
    private val jejuTrip = trip(id = 8, title = "제주 여행", start = "2026-10-03", end = "2026-10-05")

    private lateinit var db: TicketDatabase
    private lateinit var repository: TicketRepository
    private lateinit var linker: HotelScheduleLinker
    private lateinit var viewModel: TicketViewModel
    private val remote = FakeScheduleRemote()

    @Before
    fun setUp() {
        val context = ApplicationProvider.getApplicationContext<Context>()
        db = Room.inMemoryDatabaseBuilder(context, TicketDatabase::class.java).build()
        repository = TicketRepository(db.ticketDao(), ReversingCipher(), TicketAlarmScheduler(NoopBackend()))
        linker = HotelScheduleLinker(repository, remote, timeZone = { utc })
        viewModel = TicketViewModel(repository, linker)
        remote.trips = listOf(busanTrip)

        rule.setContent {
            TicketListScreen(
                viewModel = viewModel,
                highlightId = -1L,
                onAddTicket = {},
                onEditTicket = {},
                onBack = {}
            )
        }
    }

    @After
    fun tearDown() = db.close()

    // ---- 등록 직후 흐름 ----

    @Test
    fun afterRegistering_reflect_thenPickTrip_createsScheduleAndShowsLinkedBadge() {
        rule.runOnIdle { viewModel.save(draft()) {} }

        waitForText("여행 일정에 반영할까요?")
        waitUntilEnabled("반영")
        rule.onNodeWithText("확인번호와 예약자명은 공유되지 않습니다", substring = true).assertExists()
        rule.onNodeWithText("반영").performClick()

        waitForText("반영할 여행 선택")
        rule.onNodeWithText("부산 여행").performClick()

        waitForText("📅 여행 일정에 연결됨")
        assertTextGone("반영할 여행 선택")
        // 좌표가 없으면 HotelLocationSection은 지도 대신 주소 텍스트로 폴백한다.
        // 주소는 카드 윗줄(locationFrom)과 폴백 텍스트, 두 곳에 나온다.
        rule.onNodeWithTag(HOTEL_MAP_TEST_TAG).assertDoesNotExist()
        assertEquals(2, rule.onAllNodesWithText("부산 해운대구 1").fetchSemanticsNodes().size)
        val request = remote.created.single()
        assertEquals("그랜드 호텔", request.title)
        assertEquals("2026-10-02T15:00:00", request.startTime)
        assertEquals("2026-10-04T11:00:00", request.endTime)
        assertTrue(request.isHotelCheckIn)
        assertFalse(request.toString().contains("CONF-SECRET-777"))
        assertFalse(request.toString().contains("HONG GILDONG"))
        val ticket = onlyTicket()
        assertEquals(7, ticket.linkedTripId)
        assertEquals(remote.schedules.keys.single(), ticket.linkedScheduleId)
    }

    @Test
    fun afterRegistering_hotelWithCoordinates_mapComposesWithMarkerAndCamera_andLinks() {
        val name = "서울 스퀘어 호텔"
        rule.runOnIdle { viewModel.save(draft(hotel(coordinates = cityHall, name = name))) {} }

        // 등록 직후 다이얼로그는 지도가 그려진 카드 위에 떠야 한다.
        waitForText("여행 일정에 반영할까요?")
        rule.waitUntil(TIMEOUT) {
            rule.onAllNodes(SemanticsMatcher.keyIsDefined(MapCameraTarget)).fetchSemanticsNodes().isNotEmpty()
        }
        val map = rule.onNodeWithTag(HOTEL_MAP_TEST_TAG)
        map.assertExists()
        // 좌표가 있으면 주소 텍스트 폴백은 쓰지 않는다(주소는 카드 윗줄 locationFrom에만 한 번 나온다).
        assertEquals(1, rule.onAllNodesWithText("부산 해운대구 1").fetchSemanticsNodes().size)

        // 지도 SDK가 실제로 초기화되어야(Play 서비스·API 키) onMapLoaded가 불린다. 여기서 막히면 우회하지 않고 실패시킨다.
        rule.waitUntil(MAP_LOAD_TIMEOUT) {
            rule.onAllNodes(SemanticsMatcher.expectValue(MapLoaded, true)).fetchSemanticsNodes().isNotEmpty()
        }

        val config = map.fetchSemanticsNode().config
        assertEquals(name, config[MapMarkerTitle])
        assertLatLngEquals(cityHall, config[MapMarkerPosition])
        // 로드 후 cameraPositionState는 실제 지도 카메라와 동기화된 값이다.
        assertLatLngEquals(cityHall, config[MapCameraTarget])

        // 지도가 있는 상태에서도 반영 흐름이 그대로 동작하고, 좌표가 서버 요청에 실린다.
        waitUntilEnabled("반영")
        rule.onNodeWithText("반영").performClick()
        waitForText("부산 여행")
        rule.onNodeWithText("부산 여행").performClick()
        waitForText("📅 여행 일정에 연결됨")

        val request = remote.created.single()
        assertEquals(cityHall.latitude, request.latitude!!, 0.0)
        assertEquals(cityHall.longitude, request.longitude!!, 0.0)
        assertTrue(onlyTicket().isLinkedToSchedule)
        // 다이얼로그를 거치고 목록이 다시 그려진 뒤에도 지도는 예외 없이 유지된다.
        rule.onNodeWithTag(HOTEL_MAP_TEST_TAG).assertExists()
        assertEquals(name, rule.onNodeWithTag(HOTEL_MAP_TEST_TAG).fetchSemanticsNode().config[MapMarkerTitle])
    }

    @Test
    fun noTrips_reflectIsDisabledWithReason_andSkipLeavesTicketUnlinked() {
        remote.trips = emptyList()
        rule.runOnIdle { viewModel.save(draft()) {} }

        waitForText("아직 참여 중인 여행이 없어", substring = true)
        rule.onNodeWithText("반영").assertIsNotEnabled()
        rule.onNodeWithText("건너뛰기").performClick()

        assertTextGone("여행 일정에 반영할까요?")
        rule.onNodeWithText("일정에 반영").assertExists()
        assertTrue(remote.created.isEmpty())
        assertFalse(onlyTicket().isLinkedToSchedule)
    }

    @Test
    fun tripNotCoveringStay_isShownWithReason_andCannotBeChosen() {
        remote.trips = listOf(jejuTrip, busanTrip)
        addHotel()

        // 등록 직후를 건너뛴 경우의 나중 진입점(카드 버튼)
        waitForText("일정에 반영")
        rule.onNodeWithText("일정에 반영").performClick()
        waitForText("반영할 여행 선택")
        waitForText("숙박 기간이 이 여행 기간을 벗어납니다")

        rule.onNodeWithText("제주 여행").performClick()
        rule.waitForIdle()

        rule.onNodeWithText("반영할 여행 선택").assertExists()
        assertTrue(remote.created.isEmpty())
        assertFalse(onlyTicket().isLinkedToSchedule)
    }

    @Test
    fun createFails_dialogStaysOpenAndTicketStaysUnlinked() {
        remote.failWritesWith = SyncError(SyncErrorKind.REJECTED, "일정은 여행 기간 내에 등록해야 합니다.")
        addHotel()

        waitForText("일정에 반영")
        rule.onNodeWithText("일정에 반영").performClick()
        waitForText("부산 여행")
        rule.onNodeWithText("부산 여행").performClick()
        rule.waitForIdle()

        rule.onNodeWithText("반영할 여행 선택").assertExists()
        val ticket = onlyTicket()
        assertFalse(ticket.isLinkedToSchedule)
        assertEquals("CONF-SECRET-777", ticket.confirmationNumber)
    }

    // ---- 이미 연결된 티켓 ----

    @Test
    fun linkedTicket_opensManageDialogInsteadOfCreatingDuplicate_andUnlinkKeepsSchedule() {
        addLinkedHotel()

        waitForText("📅 여행 일정에 연결됨")
        rule.onNodeWithText("일정 연결 관리").performClick()
        waitForText("여행 일정에 연결됨")
        waitForText("'부산 여행'", substring = true)
        rule.onNodeWithText("반영할 여행 선택").assertDoesNotExist()

        rule.onNodeWithText("연결만 해제 (일정 유지)").performClick()

        waitForText("일정에 반영")
        assertFalse(onlyTicket().isLinkedToSchedule)
        assertEquals(1, remote.created.size)
        assertEquals(1, remote.schedules.size)
    }

    // ---- 수정 ----

    @Test
    fun editLinkedHotel_acceptUpdatesServerSchedule() {
        val ticket = addLinkedHotel()

        rule.runOnIdle {
            viewModel.startEdit(ticket)
            viewModel.save(draft(hotel(checkInTime = checkIn + hour))) {}
        }
        waitForText("여행 일정도 수정할까요?")
        rule.onNodeWithText("일정도 수정").performClick()

        rule.waitUntil(TIMEOUT) { remote.updated.isNotEmpty() }
        assertTextGone("여행 일정도 수정할까요?")
        val request = remote.updated.single()
        assertEquals("2026-10-02T16:00:00", request.startTime)
        assertTrue(request.isHotelCheckIn)
    }

    @Test
    fun editLinkedHotel_declineKeepsServerUnchangedButSavesLocally() {
        val ticket = addLinkedHotel()
        val serverBefore = remote.schedules.values.single()

        rule.runOnIdle {
            viewModel.startEdit(ticket)
            viewModel.save(draft(hotel(checkInTime = checkIn + hour))) {}
        }
        waitForText("여행 일정도 수정할까요?")
        rule.onNodeWithText("서로 달라질 수 있습니다", substring = true).assertExists()
        rule.onNodeWithText("티켓만 수정").performClick()

        assertTextGone("여행 일정도 수정할까요?")
        assertTrue(remote.updated.isEmpty())
        assertEquals(serverBefore, remote.schedules.values.single())
        val local = onlyTicket()
        assertEquals(checkIn + hour, local.hotel!!.checkInTime)
        assertTrue(local.isLinkedToSchedule)
    }

    @Test
    fun editPrivateFieldsOnly_doesNotAsk() {
        val ticket = addLinkedHotel()

        rule.runOnIdle {
            viewModel.startEdit(ticket)
            viewModel.save(draft(hotel().copy(confirmationNumber = "NEW-1"))) {}
        }
        rule.waitUntil(TIMEOUT) { runBlocking { repository.getById(ticket.id)?.confirmationNumber == "NEW-1" } }
        rule.waitForIdle()

        rule.onNodeWithText("여행 일정도 수정할까요?").assertDoesNotExist()
    }

    // ---- 삭제 ----

    @Test
    fun deleteLinked_ticketAndSchedule_removesBoth() {
        addLinkedHotel()

        waitForText("📅 여행 일정에 연결됨")
        rule.onNodeWithText("삭제").performClick()
        waitForText("연결된 일정도 함께 삭제할까요?", substring = true)
        rule.onNodeWithText("티켓과 일정 모두 삭제").performClick()

        rule.waitUntil(TIMEOUT) { ticketsNow().isEmpty() }
        assertTextGone("티켓 삭제")
        assertTrue(remote.schedules.isEmpty())
    }

    @Test
    fun deleteLinked_ticketOnly_keepsSchedule() {
        addLinkedHotel()

        waitForText("📅 여행 일정에 연결됨")
        rule.onNodeWithText("삭제").performClick()
        waitForText("티켓만 삭제")
        rule.onNodeWithText("티켓만 삭제").performClick()

        rule.waitUntil(TIMEOUT) { ticketsNow().isEmpty() }
        assertEquals(1, remote.schedules.size)
        assertTrue(remote.deleted.isEmpty())
    }

    @Test
    fun deleteLinked_serverFails_keepsTicketAndShowsError_thenTicketOnlyStillWorks() {
        addLinkedHotel()
        remote.failWritesWith = SyncError(SyncErrorKind.NETWORK)

        waitForText("📅 여행 일정에 연결됨")
        rule.onNodeWithText("삭제").performClick()
        waitForText("티켓과 일정 모두 삭제")
        rule.onNodeWithText("티켓과 일정 모두 삭제").performClick()

        waitForText("티켓은 삭제하지 않았습니다", substring = true)
        val kept = onlyTicket()
        assertTrue(kept.isLinkedToSchedule)
        assertEquals("CONF-SECRET-777", kept.confirmationNumber)

        rule.onNodeWithText("티켓만 삭제").performClick()
        rule.waitUntil(TIMEOUT) { ticketsNow().isEmpty() }
        assertEquals(1, remote.schedules.size)
    }

    // ---- helpers ----

    /** 기본은 좌표 없음(주소 텍스트 폴백 분기). 지도 분기는 [cityHall] 좌표를 넘겨 따로 테스트한다. */
    private fun hotel(
        checkInTime: Long = checkIn,
        checkOutTime: Long = checkOut,
        coordinates: LatLng? = null,
        name: String = "그랜드 호텔"
    ) = HotelDetail(
        hotelName = name,
        address = "부산 해운대구 1",
        latitude = coordinates?.latitude,
        longitude = coordinates?.longitude,
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

    /** 등록 직후 다이얼로그를 거치지 않고 저장소에 바로 넣는다(나중 진입점/연결 상태 테스트용). */
    private fun addHotel(): Long = runBlocking { repository.add(draft()) }

    private fun addLinkedHotel(): Ticket = runBlocking {
        val id = repository.add(draft())
        assertEquals(LinkOutcome.Success, linker.link(id, tripId = busanTrip.id))
        repository.getById(id)!!
    }

    private fun ticketsNow(): List<Ticket> = runBlocking {
        db.ticketDao().getUpcoming(Long.MIN_VALUE).mapNotNull { repository.getById(it.id) }
    }

    private fun onlyTicket(): Ticket = ticketsNow().single()

    private fun waitForText(text: String, substring: Boolean = false) {
        rule.waitUntil(TIMEOUT) {
            rule.onAllNodesWithText(text, substring = substring).fetchSemanticsNodes().isNotEmpty()
        }
    }

    private fun waitUntilEnabled(text: String) {
        rule.waitUntil(TIMEOUT) {
            runCatching { rule.onNodeWithText(text).assertIsEnabled() }.isSuccess
        }
    }

    private fun assertLatLngEquals(expected: LatLng, actual: LatLng) {
        // 지도 SDK는 좌표를 부동소수점으로 되돌려주므로 아주 작은 오차는 허용한다(1e-6도 ≈ 0.1m).
        assertEquals(expected.latitude, actual.latitude, 1e-6)
        assertEquals(expected.longitude, actual.longitude, 1e-6)
    }

    private fun assertTextGone(text: String) {
        rule.waitUntil(TIMEOUT) {
            rule.onAllNodesWithText(text).fetchSemanticsNodes().isEmpty()
        }
    }

    private companion object {
        const val TIMEOUT = 5_000L
        /** 첫 지도 초기화는 Play 서비스 연결 + 인증 때문에 오래 걸릴 수 있다. */
        const val MAP_LOAD_TIMEOUT = 30_000L

        /** 서울시청 */
        val cityHall = LatLng(37.566535, 126.977969)

        fun trip(id: Int, title: String, start: String, end: String) =
            TripResponse(id = id, title = title, startDate = "${start}T00:00:00", endDate = "${end}T00:00:00", createdAt = "")
    }
}
