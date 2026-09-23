package com.example.testbuild01.data.local

import androidx.room.Dao
import androidx.room.Delete
import androidx.room.Insert
import androidx.room.Query
import androidx.room.Update
import kotlinx.coroutines.flow.Flow

@Dao
interface TicketDao {
    @Query("SELECT * FROM tickets ORDER BY startDateTime ASC")
    fun observeAll(): Flow<List<TicketEntity>>

    @Query("SELECT * FROM tickets WHERE id = :id")
    suspend fun getById(id: Long): TicketEntity?

    @Query("SELECT * FROM tickets WHERE startDateTime > :now ORDER BY startDateTime ASC")
    suspend fun getUpcoming(now: Long): List<TicketEntity>

    @Insert
    suspend fun insert(ticket: TicketEntity): Long

    @Update
    suspend fun update(ticket: TicketEntity)

    /** 암호화된 상세 정보는 다시 쓰지 않고 일정 연결 정보만 바꾼다. 연결 해제는 둘 다 null. */
    @Query("UPDATE tickets SET linkedScheduleId = :scheduleId, linkedTripId = :tripId WHERE id = :id")
    suspend fun updateLink(id: Long, scheduleId: Int?, tripId: Int?)

    @Delete
    suspend fun delete(ticket: TicketEntity)
}
