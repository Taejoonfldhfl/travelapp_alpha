package com.example.testbuild01.notification

import com.example.testbuild01.data.local.TicketEntity
import com.example.testbuild01.data.local.TicketType
import com.example.testbuild01.notification.TicketAlarmScheduler.Result
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

class TicketAlarmSchedulerTest {

    private class FakeBackend(var exactAllowed: Boolean = true) : AlarmBackend {
        val exact = mutableMapOf<Long, Long>()
        val fallback = mutableMapOf<Long, Long>()
        val cancelled = mutableListOf<Long>()

        override fun canScheduleExact() = exactAllowed
        override fun scheduleExact(ticketId: Long, triggerAtMillis: Long) { exact[ticketId] = triggerAtMillis }
        override fun scheduleFallback(ticketId: Long, triggerAtMillis: Long) { fallback[ticketId] = triggerAtMillis }
        override fun cancel(ticketId: Long) {
            cancelled += ticketId
            exact.remove(ticketId)
            fallback.remove(ticketId)
        }
    }

    private val minute = 60 * 1000L
    private val hour = 60 * minute
    private val now = 1_000_000_000L
    private lateinit var backend: FakeBackend
    private lateinit var scheduler: TicketAlarmScheduler

    private fun ticket(id: Long, startsAt: Long) = TicketEntity(
        id = id,
        type = TicketType.FLIGHT,
        title = "테스트",
        startDateTime = startsAt,
        locationFrom = "ICN",
        locationTo = "NRT",
        barcodeValue = "",
        barcodeFormat = "",
        confirmationNumber = null,
        encryptedDetailsJson = ""
    )

    @Before
    fun setUp() {
        backend = FakeBackend()
        scheduler = TicketAlarmScheduler(backend, now = { now })
    }

    @Test
    fun schedule_usesExactAlarm_atStartMinusLeadTime() {
        val result = scheduler.schedule(ticket(1, now + 5 * hour))

        assertEquals(Result.EXACT, result)
        assertEquals(now + 5 * hour - TicketAlertConfig.LEAD_TIME_MINUTES * minute, backend.exact[1L])
    }

    @Test
    fun schedule_fallsBackToWorkManager_whenExactAlarmNotAllowed() {
        backend.exactAllowed = false

        val result = scheduler.schedule(ticket(1, now + 5 * hour))

        assertEquals(Result.FALLBACK, result)
        assertTrue(backend.exact.isEmpty())
        assertTrue(backend.fallback.containsKey(1L))
    }

    @Test
    fun schedule_skips_whenAlertTimeAlreadyPassed() {
        // 출발 5분 전이면 10분 전 알림 시각은 이미 지났다.
        val result = scheduler.schedule(ticket(1, now + 5 * minute))

        assertEquals(Result.SKIPPED, result)
        assertTrue(backend.exact.isEmpty() && backend.fallback.isEmpty())
    }

    @Test
    fun cancel_removesScheduledAlarm() {
        scheduler.schedule(ticket(1, now + 5 * hour))

        scheduler.cancel(1)

        assertTrue(backend.exact.isEmpty())
    }

    @Test
    fun reschedule_replacesPreviousTime() {
        scheduler.schedule(ticket(1, now + 5 * hour))

        scheduler.reschedule(ticket(1, now + 8 * hour))

        assertEquals(1, backend.exact.size)
        assertEquals(now + 8 * hour - TicketAlertConfig.LEAD_TIME_MINUTES * minute, backend.exact[1L])
    }

    @Test
    fun reschedule_toPastTime_leavesNoAlarm() {
        scheduler.schedule(ticket(1, now + 5 * hour))

        val result = scheduler.reschedule(ticket(1, now + 5 * minute))

        assertEquals(Result.SKIPPED, result)
        assertTrue(backend.exact.isEmpty())
    }

    @Test
    fun schedule_keepsTicketsIndependent() {
        scheduler.schedule(ticket(1, now + 5 * hour))
        scheduler.schedule(ticket(2, now + 6 * hour))

        scheduler.cancel(1)

        assertEquals(setOf(2L), backend.exact.keys)
    }

    @Test
    fun schedule_switchesFromFallbackToExact_withoutLeavingStaleWork() {
        backend.exactAllowed = false
        scheduler.schedule(ticket(1, now + 5 * hour))

        backend.exactAllowed = true
        scheduler.schedule(ticket(1, now + 5 * hour))

        assertTrue(backend.fallback.isEmpty())
        assertTrue(backend.exact.containsKey(1L))
    }
}
