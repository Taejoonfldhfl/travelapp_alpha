package com.example.testbuild01.notification

import android.app.AlarmManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import androidx.work.ExistingWorkPolicy
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.workDataOf
import java.util.concurrent.TimeUnit

class AndroidAlarmBackend(context: Context) : AlarmBackend {
    private val appContext = context.applicationContext
    private val alarmManager = appContext.getSystemService(AlarmManager::class.java)

    override fun canScheduleExact(): Boolean =
        Build.VERSION.SDK_INT < Build.VERSION_CODES.S || alarmManager.canScheduleExactAlarms()

    override fun scheduleExact(ticketId: Long, triggerAtMillis: Long) {
        alarmManager.setExactAndAllowWhileIdle(
            AlarmManager.RTC_WAKEUP,
            triggerAtMillis,
            alarmIntent(ticketId)
        )
    }

    override fun scheduleFallback(ticketId: Long, triggerAtMillis: Long) {
        val delay = (triggerAtMillis - System.currentTimeMillis()).coerceAtLeast(0)
        val request = OneTimeWorkRequestBuilder<TicketAlertWorker>()
            .setInitialDelay(delay, TimeUnit.MILLISECONDS)
            .setInputData(workDataOf(TicketAlertWorker.KEY_TICKET_ID to ticketId))
            .build()
        WorkManager.getInstance(appContext)
            .enqueueUniqueWork(workName(ticketId), ExistingWorkPolicy.REPLACE, request)
    }

    override fun cancel(ticketId: Long) {
        alarmManager.cancel(alarmIntent(ticketId))
        WorkManager.getInstance(appContext).cancelUniqueWork(workName(ticketId))
    }

    private fun workName(ticketId: Long) = "ticket_alert_$ticketId"

    // 티켓 id를 data URI에 넣어 티켓마다 서로 다른 PendingIntent가 되도록 한다.
    private fun alarmIntent(ticketId: Long): PendingIntent {
        val intent = Intent(appContext, TicketAlarmReceiver::class.java).apply {
            action = TicketAlarmReceiver.ACTION_TICKET_ALARM
            data = Uri.parse("ticket://alarm/$ticketId")
            putExtra(TicketAlarmReceiver.EXTRA_TICKET_ID, ticketId)
        }
        return PendingIntent.getBroadcast(
            appContext,
            ticketId.toInt(),
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
    }
}
