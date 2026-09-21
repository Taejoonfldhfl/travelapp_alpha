package com.example.testbuild01.notification

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.example.testbuild01.MainApplication

/** 정확한 알람 권한이 없을 때 쓰는 폴백. 실행 시각이 지연될 수 있다. */
class TicketAlertWorker(context: Context, params: WorkerParameters) : CoroutineWorker(context, params) {
    override suspend fun doWork(): Result {
        val ticketId = inputData.getLong(KEY_TICKET_ID, -1L)
        if (ticketId < 0) return Result.failure()

        val repository = (applicationContext as MainApplication).ticketRepository
        repository.getById(ticketId)?.let { TicketNotifier.show(applicationContext, it) }
        return Result.success()
    }

    companion object {
        const val KEY_TICKET_ID = "ticket_id"
    }
}
