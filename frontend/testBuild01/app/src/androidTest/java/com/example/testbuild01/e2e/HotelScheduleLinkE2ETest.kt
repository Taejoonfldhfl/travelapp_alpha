package com.example.testbuild01.e2e

import android.content.Context
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.example.testbuild01.data.local.HotelDetail
import com.example.testbuild01.data.local.TicketCipher
import com.example.testbuild01.data.local.TicketDatabase
import com.example.testbuild01.data.local.TicketType
import com.example.testbuild01.data.local.TokenManager
import com.example.testbuild01.data.model.LoginRequest
import com.example.testbuild01.data.model.ScheduleResponse
import com.example.testbuild01.data.model.Ticket
import com.example.testbuild01.data.network.RetrofitClient
import com.example.testbuild01.data.network.RetrofitScheduleRemote
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
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Assume.assumeTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import java.util.TimeZone

/**
 * 실제 백엔드(10.0.2.2:5020)에 붙는 기능(E2E) 테스트. 화면·ViewModel·linker·RetrofitClient를 그대로 쓰고
 * 서버에 실제로 반영된 일정을 API로 다시 조회해 확인한다.
 *
 * 서버와 테스트 계정이 필요하므로 인자가 없으면 건너뛴다:
 *   ./gradlew connectedDebugAndroidTest \
 *     -Pandroid.testInstrumentationRunnerArguments.class=com.example.testbuild01.e2e.HotelScheduleLinkE2ETest \
 *     -Pandroid.testInstrumentationRunnerArguments.e2eEmail=... \
 *     -Pandroid.testInstrumentationRunnerArguments.e2ePassword=... \
 *     -Pandroid.testInstrumentationRunnerArguments.e2eTripId=...
 * 계정에는 2026-10-01 ~ 2026-10-05 기간의 여행(e2eTripId) 하나만 있어야 한다.
 */
@RunWith(AndroidJUnit4::class)
class HotelScheduleLinkE2ETest {

    @get:Rule
    val rule = createComposeRule()

    private class NoopBackend : AlarmBackend {
        override fun canScheduleExact() = true
        override fun scheduleExact(ticketId: Long, slot: AlarmSlot, triggerAtMillis: Long) = Unit
        override fun scheduleFallback(ticketId: Long, slot: AlarmSlot, triggerAtMillis: Long) = Unit
        override fun cancel(ticketId: Long, slot: AlarmSlot) = Unit
    }

    private class ReversingCipher : TicketCipher {
        override fun encrypt(plainText: String) = "enc:" + plainText.reversed()
        override fun decrypt(cipherText: String) = cipherText.removePrefix("enc:").reversed()
    }

    private val args = InstrumentationRegistry.getArguments()
    private val email: String? = args.getString("e2eEmail")
    private val password: String? = args.getString("e2ePassword")
    private val tripId: Int = args.getString("e2eTripId")?.toIntOrNull() ?: -1

    private val context = ApplicationProvider.getApplicationContext<Context>()
    private val tokenManager = TokenManager(context)
    private var previousToken: String? = null

    private val utc = TimeZone.getTimeZone("UTC")
    // 2026-10-02 15:00 UTC 체크인, 2026-10-04 11:00 UTC 체크아웃 (linker를 UTC로 고정해 서버 문자열이 결정적이다)
    private val checkIn = 1_790_953_200_000L
    private val checkOut = 1_791_111_600_000L
    private val hour = 3_600_000L
    private val hotelName = "E2E 서울 스퀘어 호텔 ${System.currentTimeMillis() % 100_000}"

    private lateinit var db: TicketDatabase
    private lateinit var repository: TicketRepository
    private lateinit var linker: HotelScheduleLinker
    private lateinit var viewModel: TicketViewModel
    private lateinit var tripTitle: String

    @Before
    fun setUp() {
        assumeTrue("E2E 인자(e2eEmail/e2ePassword/e2eTripId)가 없어 건너뜀", email != null && password != null && tripId > 0)

        // 에뮬레이터 앱의 기존 로그인 토큰은 테스트 후 되돌린다.
        previousToken = tokenManager.getToken()
        val login = RetrofitClient.instance.login(LoginRequest(email!!, password!!)).execute()
        assertTrue("로그인 실패: HTTP ${login.code()}", login.isSuccessful)
        tokenManager.saveToken(login.body()!!.token)

        val trips = runBlocking { RetrofitClient.instance.getTripsSuspend() }.body()!!
        tripTitle = trips.single { it.id == tripId }.title
        deleteServerSchedulesNamed(hotelName)

        db = Room.inMemoryDatabaseBuilder(context, TicketDatabase::class.java).build()
        repository = TicketRepository(db.ticketDao(), ReversingCipher(), TicketAlarmScheduler(NoopBackend()))
        linker = HotelScheduleLinker(repository, RetrofitScheduleRemote(), timeZone = { utc })
        viewModel = TicketViewModel(repository, linker)

        rule.setContent {
            TicketListScreen(viewModel = viewModel, highlightId = -1L, onAddTicket = {}, onEditTicket = {}, onBack = {})
        }
    }

    @After
    fun tearDown() {
        if (!::db.isInitialized) return
        deleteServerSchedulesNamed(hotelName)
        db.close()
        previousToken?.let { tokenManager.saveToken(it) } ?: tokenManager.clearToken()
    }

    /** 등록 → 반영 → 수정(수락/거부) → 관리 → 삭제까지 한 호텔로 실제 서버와 이어서 확인한다. */
    @Test
    fun fullFlow_againstRealServer() {
        // 1) 등록 직후 "반영할까요?" → 실제 서버의 여행 목록에서 선택
        rule.runOnIdle { viewModel.save(draft(hotel())) {} }
        waitForText("여행 일정에 반영할까요?")
        waitUntilEnabled("반영")
        rule.onNodeWithText("반영").performClick()
        waitForText(tripTitle)
        rule.onNodeWithText(tripTitle).performClick()
        waitForText("📅 여행 일정에 연결됨", timeout = 15_000)

        val linked = onlyTicket()
        assertEquals(tripId, linked.linkedTripId)
        val created = serverSchedule(linked.linkedScheduleId!!)
        assertEquals(hotelName, created.title)
        assertEquals("서울 중구 세종대로 110", created.placeName)
        assertEquals("", created.description)
        assertEquals("2026-10-02T15:00:00", created.startTime.take(19))
        assertEquals("2026-10-04T11:00:00", created.endTime.take(19))
        assertEquals(37.566535, created.latitude!!, 1e-9)
        assertEquals(126.977969, created.longitude!!, 1e-9)
        assertTrue(created.isHotelCheckIn)

        // 2) 체크인 시각 수정 → "일정도 수정" → 서버 반영, 좌표·체크인 플래그 유지
        editHotel(linked, hotel(checkInTime = checkIn + hour))
        waitForText("여행 일정도 수정할까요?")
        rule.onNodeWithText("일정도 수정").performClick()
        waitUntil { serverSchedule(linked.linkedScheduleId).startTime.startsWith("2026-10-02T16:00:00") }
        val updated = serverSchedule(linked.linkedScheduleId)
        assertEquals(37.566535, updated.latitude!!, 1e-9)
        assertTrue(updated.isHotelCheckIn)

        // 3) 다시 수정 → "티켓만 수정" → 서버는 그대로
        editHotel(onlyTicket(), hotel(checkInTime = checkIn + 2 * hour))
        waitForText("여행 일정도 수정할까요?")
        rule.onNodeWithText("티켓만 수정").performClick()
        assertTextGone("여행 일정도 수정할까요?")
        assertEquals(checkIn + 2 * hour, onlyTicket().hotel!!.checkInTime)
        assertTrue(serverSchedule(linked.linkedScheduleId).startTime.startsWith("2026-10-02T16:00:00"))

        // 4) 카드의 "일정 연결 관리" → 서버에서 받은 여행 제목이 보이고 새 일정은 만들지 않는다
        rule.onNodeWithText("일정 연결 관리").performClick()
        waitForText("'$tripTitle'", substring = true)
        rule.onNodeWithText("현재 호텔 정보로 일정 갱신").performClick()
        waitUntil { serverSchedule(linked.linkedScheduleId).startTime.startsWith("2026-10-02T17:00:00") }
        assertEquals(1, serverSchedulesNamed(hotelName).size)

        // 5) 삭제 → "티켓과 일정 모두 삭제" → 서버에서도 사라진다
        rule.onNodeWithText("삭제").performClick()
        waitForText("티켓과 일정 모두 삭제")
        rule.onNodeWithText("티켓과 일정 모두 삭제").performClick()
        waitUntil { ticketsNow().isEmpty() }
        val gone = runBlocking { RetrofitClient.instance.getScheduleSuspend(tripId, linked.linkedScheduleId) }
        assertEquals(404, gone.code())
    }

    /** 연결 정보만 잃은 뒤(재설치 등) 다시 반영하면 서버의 같은 일정에 붙고 중복을 만들지 않는다. */
    @Test
    fun relinkAfterLocalLinkLost_reusesServerSchedule() {
        val id = runBlocking { repository.add(draft(hotel())) }
        assertEquals(LinkOutcome.Success, runBlocking { linker.link(id, tripId) })
        val firstScheduleId = runBlocking { repository.getById(id)!!.linkedScheduleId!! }

        runBlocking { repository.clearScheduleLink(id) }
        assertEquals(LinkOutcome.Success, runBlocking { linker.link(id, tripId) })

        assertEquals(firstScheduleId, runBlocking { repository.getById(id)!!.linkedScheduleId })
        assertEquals(1, serverSchedulesNamed(hotelName).size)
    }

    /** 서버의 실제 거부 응답이 앱의 오류 분류·문구로 제대로 바뀌는지, 그리고 로컬은 그대로인지. */
    @Test
    fun realServerRejections_areClassifiedAndLocalTicketKept() {
        // 여행 기간(10/01~10/05) 밖 → 서버 400 + 평문 사유
        val outside = runBlocking {
            repository.add(draft(hotel(checkInTime = checkIn + 10 * 24 * hour, checkOutTime = checkOut + 10 * 24 * hour)))
        }
        val rejected = runBlocking { linker.link(outside, tripId) } as LinkOutcome.Failed
        assertEquals(SyncErrorKind.REJECTED, rejected.error.kind)
        assertEquals("일정은 여행 기간 내에 등록해야 합니다.", rejected.error.serverMessage)
        assertFalse(runBlocking { repository.getById(outside)!!.isLinkedToSchedule })

        // 멤버가 아닌 여행 → 서버 403
        val notMember = runBlocking { repository.add(draft(hotel())) }
        val forbidden = runBlocking { linker.link(notMember, Int.MAX_VALUE) } as LinkOutcome.Failed
        assertEquals(SyncErrorKind.FORBIDDEN, forbidden.error.kind)
        assertEquals("CONF-SECRET-777", runBlocking { repository.getById(notMember)!!.confirmationNumber })
        assertTrue(serverSchedulesNamed(hotelName).isEmpty())
    }

    // ---- helpers ----

    private fun hotel(checkInTime: Long = checkIn, checkOutTime: Long = checkOut) = HotelDetail(
        hotelName = hotelName,
        address = "서울 중구 세종대로 110",
        latitude = 37.566535,
        longitude = 126.977969,
        checkInTime = checkInTime,
        checkOutTime = checkOutTime,
        roomType = "디럭스",
        guestCount = 2,
        confirmationNumber = "CONF-SECRET-777",
        guestNameOnBooking = "HONG GILDONG",
        freeCancellationDeadline = null,
        phoneNumber = null
    )

    private fun draft(detail: HotelDetail) = TicketDraft(
        type = TicketType.HOTEL,
        title = detail.hotelName,
        startDateTime = detail.checkInTime,
        locationFrom = detail.address,
        locationTo = "",
        confirmationNumber = detail.confirmationNumber,
        hotel = detail
    )

    private fun editHotel(ticket: Ticket, detail: HotelDetail) {
        rule.runOnIdle {
            viewModel.startEdit(ticket)
            viewModel.save(draft(detail)) {}
        }
    }

    private fun serverSchedule(scheduleId: Int?): ScheduleResponse {
        val response = runBlocking { RetrofitClient.instance.getScheduleSuspend(tripId, scheduleId!!) }
        assertTrue("일정 조회 실패: HTTP ${response.code()}", response.isSuccessful)
        return response.body()!!
    }

    private fun serverSchedulesNamed(title: String): List<ScheduleResponse> =
        runBlocking { RetrofitClient.instance.getSchedulesSuspend(tripId) }.body().orEmpty().filter { it.title == title }

    private fun deleteServerSchedulesNamed(title: String) {
        serverSchedulesNamed(title).forEach { runBlocking { RetrofitClient.instance.deleteScheduleSuspend(tripId, it.id) } }
    }

    private fun ticketsNow(): List<Ticket> = runBlocking {
        db.ticketDao().getUpcoming(Long.MIN_VALUE).mapNotNull { repository.getById(it.id) }
    }

    private fun onlyTicket(): Ticket = ticketsNow().single()

    private fun waitUntil(timeout: Long = 15_000, condition: () -> Boolean) = rule.waitUntil(timeout, condition)

    private fun waitForText(text: String, substring: Boolean = false, timeout: Long = 15_000) = waitUntil(timeout) {
        rule.onAllNodesWithText(text, substring = substring).fetchSemanticsNodes().isNotEmpty()
    }

    private fun waitUntilEnabled(text: String) = waitUntil {
        runCatching { rule.onNodeWithText(text).assertIsEnabled() }.isSuccess
    }

    private fun assertTextGone(text: String) = waitUntil {
        rule.onAllNodesWithText(text).fetchSemanticsNodes().isEmpty()
    }
}
