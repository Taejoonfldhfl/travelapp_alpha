package com.example.testbuild01.data.local

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase

@Database(entities = [TicketEntity::class], version = 1, exportSchema = false)
abstract class TicketDatabase : RoomDatabase() {
    abstract fun ticketDao(): TicketDao

    companion object {
        // backup_rules.xml / data_extraction_rules.xml 의 제외 대상 파일명과 일치해야 한다.
        const val DB_NAME = "ticket.db"

        fun create(context: Context): TicketDatabase =
            Room.databaseBuilder(context.applicationContext, TicketDatabase::class.java, DB_NAME)
                .build()
    }
}
