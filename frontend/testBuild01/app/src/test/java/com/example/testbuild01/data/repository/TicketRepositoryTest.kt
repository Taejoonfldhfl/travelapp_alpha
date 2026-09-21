package com.example.testbuild01.data.repository

import com.example.testbuild01.data.local.TicketCipher
import com.example.testbuild01.data.local.TicketDao
import com.example.testbuild01.data.local.TicketEntity
import com.example.testbuild01.data.local.TicketType
import com.example.testbuild01.notification.AlarmBackend
import com.example.testbuild01.notification.TicketAlarmScheduler
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** insert/update/delete가 알림 예약·재등록·취소로 이어지는지 확인한다. */
class TicketRepositoryTest {

    private class FakeDao : TicketDao {
        val rows = mutableMapOf<Long, TicketEntity>()
        private var nextId = 1L

        override fun observeAll(): Flow<List<TicketEntity>> = flowOf(rows.values.sortedBy { it.startDateTime })
        override suspend fun getById(id: Long) = rows[id]
        override suspend fun getUpcoming(now: Long) = rows.values.filter { it.startDateTime > now }
        override suspend fun insert(ticket: TicketEntity): Long {
            val id = nextId++
            rows[id] = ticket.copy(id = id)
            return id
        }
        override suspend fun update(ticket: TicketEntity) { rows[ticket.id] = ticket }
        override suspend fun delete(ticket: TicketEntity) { rows.remove(ticket.id) }
    }

    private class FakeBackend : AlarmBackend {
        val exact = mutableMapOf<Long, Long>()
        override fun canScheduleExact() = true
        override fun scheduleExact(ticketId: Long, triggerAtMillis: Long) { exact[ticketId] = triggerAtMillis }
        override fun scheduleFallback(ticketId: Long, triggerAtMillis: Long) = Unit
        override fun cancel(ticketId: Long) { exact.remove(ticketId) }
    }

    private class ReversingCipher : TicketCipher {
        override fun encrypt(plainText: String) = "enc:" + plainText.reversed()
        override fun decrypt(cipherText: String) = cipherText.removePrefix("enc:").reversed()
    }

    private class BrokenCipher : TicketCipher {
        override fun encrypt(plainText: String) = "enc:" + plainText.reversed()
        override fun decrypt(cipherText: String): String = error("키셋을 찾을 수 없음")
    }

    private val hour = 60 * 60 * 1000L
    private val now = 1_000_000_000L
    private val dao = FakeDao()
    private val backend = FakeBackend()
    private val repository = TicketRepository(
        dao = dao,
        cipher = ReversingCipher(),
        scheduler = TicketAlarmScheduler(backend, now = { now }),
        now = { now }
    )

    private fun draft(startsAt: Long) = TicketDraft(
        type = TicketType.BUS,
        title = "서울-부산",
        startDateTime = startsAt,
        locationFrom = "서울",
        locationTo = "부산",
        barcodeValue = "QR-SECRET-999",
        barcodeFormat = "QR_CODE",
        confirmationNumber = "ABC123"
    )

    @Test
    fun add_schedulesAlarmUsingInsertedId() = runTest {
        val id = repository.add(draft(now + 5 * hour))

        assertTrue(backend.exact.containsKey(id))
    }

    @Test
    fun add_storesEncryptedDetails_notPlainText() = runTest {
        val id = repository.add(draft(now + 5 * hour))

        val stored = dao.rows.getValue(id).encryptedDetailsJson
        assertTrue(stored.startsWith("enc:"))
        assertFalse(stored.contains("ABC123"))
    }

    @Test
    fun add_leavesPlainTextColumnsEmpty() = runTest {
        val id = repository.add(draft(now + 5 * hour))

        val row = dao.rows.getValue(id)
        assertEquals("", row.barcodeValue)
        assertEquals("", row.barcodeFormat)
        assertNull(row.confirmationNumber)
    }

    @Test
    fun observeAll_returnsDecryptedDetails() = runTest {
        repository.add(draft(now + 5 * hour))

        val ticket = repository.observeAll().first().single()

        assertEquals("QR-SECRET-999", ticket.barcodeValue)
        assertEquals("QR_CODE", ticket.barcodeFormat)
        assertEquals("ABC123", ticket.confirmationNumber)
    }

    @Test
    fun getById_returnsDecryptedDetails() = runTest {
        val id = repository.add(draft(now + 5 * hour))

        assertEquals("ABC123", repository.getById(id)?.confirmationNumber)
    }

    @Test
    fun read_whenDecryptionFails_stillReturnsTicketWithEmptyDetails() = runTest {
        val broken = TicketRepository(
            dao = dao,
            cipher = BrokenCipher(),
            scheduler = TicketAlarmScheduler(backend, now = { now }),
            now = { now }
        )
        val id = broken.add(draft(now + 5 * hour))

        val ticket = broken.getById(id)!!

        assertEquals("서울-부산", ticket.title)
        assertEquals("", ticket.barcodeValue)
        assertNull(ticket.confirmationNumber)
    }

    @Test
    fun update_reschedulesAtNewTime() = runTest {
        val id = repository.add(draft(now + 5 * hour))
        val before = backend.exact.getValue(id)

        repository.update(id, draft(now + 9 * hour))

        assertNotEquals(before, backend.exact.getValue(id))
        assertEquals(1, backend.exact.size)
    }

    @Test
    fun delete_cancelsAlarm() = runTest {
        val id = repository.add(draft(now + 5 * hour))

        repository.delete(repository.getById(id)!!)

        assertTrue(backend.exact.isEmpty())
        assertTrue(dao.rows.isEmpty())
    }

    @Test
    fun rescheduleUpcoming_restoresAlarmsAfterReboot() = runTest {
        val id = repository.add(draft(now + 5 * hour))
        backend.exact.clear() // 재부팅으로 예약이 사라진 상황

        repository.rescheduleUpcoming()

        assertTrue(backend.exact.containsKey(id))
    }
}
