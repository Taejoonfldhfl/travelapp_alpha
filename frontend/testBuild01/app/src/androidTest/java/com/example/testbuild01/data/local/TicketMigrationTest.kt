package com.example.testbuild01.data.local

import android.content.ContentValues
import android.database.sqlite.SQLiteDatabase
import androidx.room.Room
import androidx.room.testing.MigrationTestHelper
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/** v2 → v3: 기존 티켓(암호화 JSON 포함)은 그대로 두고 일정 연결 컬럼만 null로 추가되는지 확인한다. */
@RunWith(AndroidJUnit4::class)
class TicketMigrationTest {

    private val dbName = "migration-test.db"

    @get:Rule
    val helper = MigrationTestHelper(
        InstrumentationRegistry.getInstrumentation(),
        TicketDatabase::class.java
    )

    @Test
    fun migrate2To3_keepsExistingRowsAndAddsNullLink() {
        helper.createDatabase(dbName, 2).apply {
            insert("tickets", SQLiteDatabase.CONFLICT_NONE, ContentValues().apply {
                put("id", 1L)
                put("type", TicketType.HOTEL.name)
                put("title", "그랜드 호텔")
                put("startDateTime", 1_000L)
                put("locationFrom", "부산")
                put("locationTo", "")
                put("barcodeValue", "")
                put("barcodeFormat", "")
                putNull("confirmationNumber")
                put("encryptedDetailsJson", "enc:secret")
                put("secondaryAlertAt", 500L)
            })
            close()
        }

        val db = helper.runMigrationsAndValidate(dbName, 3, true, TicketDatabase.MIGRATION_2_3)

        db.query("SELECT title, encryptedDetailsJson, secondaryAlertAt, linkedScheduleId, linkedTripId FROM tickets").use {
            it.moveToFirst()
            assertEquals("그랜드 호텔", it.getString(0))
            assertEquals("enc:secret", it.getString(1))
            assertEquals(500L, it.getLong(2))
            assertEquals(true, it.isNull(3))
            assertEquals(true, it.isNull(4))
        }
    }

    @Test
    fun afterMigration_linkCanBeSavedAndCleared() = runTest {
        helper.createDatabase(dbName, 2).apply {
            execSQL(
                "INSERT INTO tickets (id, type, title, startDateTime, locationFrom, locationTo, barcodeValue, barcodeFormat, " +
                    "confirmationNumber, encryptedDetailsJson, secondaryAlertAt) " +
                    "VALUES (1, 'HOTEL', '그랜드 호텔', 1000, '부산', '', '', '', NULL, 'enc', NULL)"
            )
            close()
        }
        helper.runMigrationsAndValidate(dbName, 3, true, TicketDatabase.MIGRATION_2_3).close()

        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val room = Room.databaseBuilder(context, TicketDatabase::class.java, dbName)
            .addMigrations(TicketDatabase.MIGRATION_2_3)
            .build()
        try {
            val dao = room.ticketDao()
            assertNull(dao.getById(1)!!.linkedScheduleId)

            dao.updateLink(1, scheduleId = 42, tripId = 7)
            val linked = dao.getById(1)!!
            assertEquals(42, linked.linkedScheduleId)
            assertEquals(7, linked.linkedTripId)
            assertEquals("enc", linked.encryptedDetailsJson)

            dao.updateLink(1, scheduleId = null, tripId = null)
            assertNull(dao.getById(1)!!.linkedScheduleId)
            assertNull(dao.getById(1)!!.linkedTripId)
        } finally {
            room.close()
        }
    }
}
