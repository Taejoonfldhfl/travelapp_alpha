package com.example.testbuild01.data.local

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class TicketDaoTest {

    private lateinit var db: TicketDatabase
    private lateinit var dao: TicketDao

    private fun ticket(title: String, startsAt: Long) = TicketEntity(
        type = TicketType.BUS,
        title = title,
        startDateTime = startsAt,
        locationFrom = "서울",
        locationTo = "부산",
        barcodeValue = "",
        barcodeFormat = "",
        confirmationNumber = null,
        encryptedDetailsJson = "enc"
    )

    @Before
    fun setUp() {
        val context = ApplicationProvider.getApplicationContext<Context>()
        db = Room.inMemoryDatabaseBuilder(context, TicketDatabase::class.java).build()
        dao = db.ticketDao()
    }

    @After
    fun tearDown() = db.close()

    @Test
    fun insert_thenGetById_returnsTicket() = runTest {
        val id = dao.insert(ticket("A", 1_000))

        assertEquals("A", dao.getById(id)?.title)
    }

    @Test
    fun observeAll_isSortedByStartDateTimeAscending() = runTest {
        dao.insert(ticket("late", 3_000))
        dao.insert(ticket("early", 1_000))
        dao.insert(ticket("middle", 2_000))

        assertEquals(listOf("early", "middle", "late"), dao.observeAll().first().map { it.title })
    }

    @Test
    fun update_changesStoredValues() = runTest {
        val id = dao.insert(ticket("A", 1_000))

        dao.update(ticket("A2", 5_000).copy(id = id))

        assertEquals(5_000L, dao.getById(id)?.startDateTime)
    }

    @Test
    fun delete_removesTicket() = runTest {
        val id = dao.insert(ticket("A", 1_000))

        dao.delete(dao.getById(id)!!)

        assertNull(dao.getById(id))
    }

    @Test
    fun getUpcoming_excludesPastTickets() = runTest {
        dao.insert(ticket("past", 1_000))
        dao.insert(ticket("future", 9_000))

        assertEquals(listOf("future"), dao.getUpcoming(now = 5_000).map { it.title })
    }
}
