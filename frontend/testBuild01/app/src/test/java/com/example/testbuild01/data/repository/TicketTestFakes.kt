package com.example.testbuild01.data.repository

import com.example.testbuild01.data.local.TicketCipher
import com.example.testbuild01.data.local.TicketDao
import com.example.testbuild01.data.local.TicketEntity
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf

/** Room 없이 TicketRepository를 테스트하기 위한 메모리 DAO */
class FakeTicketDao : TicketDao {
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
    override suspend fun updateLink(id: Long, scheduleId: Int?, tripId: Int?) {
        rows[id]?.let { rows[id] = it.copy(linkedScheduleId = scheduleId, linkedTripId = tripId) }
    }
    override suspend fun delete(ticket: TicketEntity) { rows.remove(ticket.id) }
}

class ReversingCipher : TicketCipher {
    override fun encrypt(plainText: String) = "enc:" + plainText.reversed()
    override fun decrypt(cipherText: String) = cipherText.removePrefix("enc:").reversed()
}
