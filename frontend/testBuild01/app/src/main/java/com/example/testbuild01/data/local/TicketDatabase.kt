package com.example.testbuild01.data.local

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

@Database(entities = [TicketEntity::class], version = 3, exportSchema = true)
abstract class TicketDatabase : RoomDatabase() {
    abstract fun ticketDao(): TicketDao

    companion object {
        // backup_rules.xml / data_extraction_rules.xml 의 제외 대상 파일명과 일치해야 한다.
        const val DB_NAME = "ticket.db"

        /** 호텔 무료취소 마감 알림용 평문 컬럼 추가 */
        private val MIGRATION_1_2 = object : Migration(1, 2) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE tickets ADD COLUMN secondaryAlertAt INTEGER")
            }
        }

        /** 호텔 티켓 ↔ 서버 일정 연결용 평문 컬럼 추가. 기존 티켓은 모두 미연결(null)로 시작한다. */
        internal val MIGRATION_2_3 = object : Migration(2, 3) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE tickets ADD COLUMN linkedScheduleId INTEGER")
                db.execSQL("ALTER TABLE tickets ADD COLUMN linkedTripId INTEGER")
            }
        }

        fun create(context: Context): TicketDatabase =
            Room.databaseBuilder(context.applicationContext, TicketDatabase::class.java, DB_NAME)
                .addMigrations(MIGRATION_1_2, MIGRATION_2_3)
                .build()
    }
}
