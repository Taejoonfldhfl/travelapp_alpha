package com.example.testbuild01.notification

import android.annotation.SuppressLint
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.example.testbuild01.MainActivity
import com.example.testbuild01.data.model.Ticket

object TicketNotifier {

    fun createChannel(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val channel = NotificationChannel(
            TicketAlertConfig.CHANNEL_ID,
            "티켓 알림",
            NotificationManager.IMPORTANCE_HIGH
        ).apply { description = "버스/항공권/호텔 예약 알림" }
        context.getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }

    @SuppressLint("MissingPermission")
    fun show(context: Context, ticket: Ticket, slot: AlarmSlot = AlarmSlot.PRIMARY) {
        val manager = NotificationManagerCompat.from(context)
        // POST_NOTIFICATIONS 거부 또는 알림 차단 상태면 조용히 건너뛴다.
        if (!manager.areNotificationsEnabled()) return

        val (title, text) = contentFor(ticket, slot)
        val contentIntent = PendingIntent.getActivity(
            context,
            notificationId(ticket.id, slot),
            Intent(context, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
                putExtra(MainActivity.EXTRA_TICKET_ID, ticket.id)
            },
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val notification = NotificationCompat.Builder(context, TicketAlertConfig.CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_lock_idle_alarm)
            .setContentTitle(title)
            .setContentText(text)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setCategory(NotificationCompat.CATEGORY_REMINDER)
            .setAutoCancel(true)
            .setContentIntent(contentIntent)
            .build()

        manager.notify(notificationId(ticket.id, slot), notification)
    }

    private fun contentFor(ticket: Ticket, slot: AlarmSlot): Pair<String, String> = when (slot) {
        AlarmSlot.PRIMARY -> {
            val verb = TicketAlertConfig.verbFor(ticket.type)
            "$verb ${TicketAlertConfig.LEAD_TIME_MINUTES}분 전입니다" to
                "${ticket.title} · ${ticket.locationFrom} → ${ticket.locationTo}"
        }
        AlarmSlot.SECONDARY -> {
            "무료 취소 마감 ${TicketAlertConfig.LEAD_TIME_MINUTES}분 전입니다" to
                "${ticket.title} 예약의 무료취소 기한이 얼마 남지 않았습니다."
        }
    }

    // 슬롯별로 알림/PendingIntent id가 겹치지 않게 한다 (AndroidAlarmBackend의 request code 규칙과 동일).
    private fun notificationId(ticketId: Long, slot: AlarmSlot): Int = (ticketId * 2 + slot.ordinal).toInt()
}
