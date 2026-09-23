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

    override fun scheduleExact(ticketId: Long, slot: AlarmSlot, triggerAtMillis: Long) {
        alarmManager.setExactAndAllowWhileIdle(
            AlarmManager.RTC_WAKEUP,
            triggerAtMillis,
            alarmIntent(ticketId, slot)
        )
    }

    override fun scheduleFallback(ticketId: Long, slot: AlarmSlot, triggerAtMillis: Long) {
        val delay = (triggerAtMillis - System.currentTimeMillis()).coerceAtLeast(0)
        val request = OneTimeWorkRequestBuilder<TicketAlertWorker>()
            .setInitialDelay(delay, TimeUnit.MILLISECONDS)
            .setInputData(
                workDataOf(
                    TicketAlertWorker.KEY_TICKET_ID to ticketId,
                    TicketAlertWorker.KEY_SLOT to slot.name
                )
            )
            .build()
        WorkManager.getInstance(appContext)
            .enqueueUniqueWork(workName(ticketId, slot), ExistingWorkPolicy.REPLACE, request)
    }

    override fun cancel(ticketId: Long, slot: AlarmSlot) {
        alarmManager.cancel(alarmIntent(ticketId, slot))
        WorkManager.getInstance(appContext).cancelUniqueWork(workName(ticketId, slot))
    }

    private fun workName(ticketId: Long, slot: AlarmSlot) = "ticket_alert_${ticketId}_${slot.name}"

    // 티켓 id + 슬롯을 request code와 data URI에 넣어 슬롯마다 서로 다른 PendingIntent가 되도록 한다.
    private fun requestCode(ticketId: Long, slot: AlarmSlot): Int = (ticketId * 2 + slot.ordinal).toInt()

    private fun alarmIntent(ticketId: Long, slot: AlarmSlot): PendingIntent {
        val intent = Intent(appContext, TicketAlarmReceiver::class.java).apply {
            action = TicketAlarmReceiver.ACTION_TICKET_ALARM
            data = Uri.parse("ticket://alarm/$ticketId/${slot.name}")
            putExtra(TicketAlarmReceiver.EXTRA_TICKET_ID, ticketId)
            putExtra(TicketAlarmReceiver.EXTRA_SLOT, slot.name)
        }
        return PendingIntent.getBroadcast(
            appContext,
            requestCode(ticketId, slot),
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
    }
}
