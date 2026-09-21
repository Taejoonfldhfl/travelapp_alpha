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
        ).apply { description = "버스/항공권 탑승 임박 알림" }
        context.getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }

    @SuppressLint("MissingPermission")
    fun show(context: Context, ticket: Ticket) {
        val manager = NotificationManagerCompat.from(context)
        // POST_NOTIFICATIONS 거부 또는 알림 차단 상태면 조용히 건너뛴다.
        if (!manager.areNotificationsEnabled()) return

        val verb = TicketAlertConfig.verbFor(ticket.type)
        val contentIntent = PendingIntent.getActivity(
            context,
            ticket.id.toInt(),
            Intent(context, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
                putExtra(MainActivity.EXTRA_TICKET_ID, ticket.id)
            },
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val notification = NotificationCompat.Builder(context, TicketAlertConfig.CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_lock_idle_alarm)
            .setContentTitle("$verb ${TicketAlertConfig.LEAD_TIME_MINUTES}분 전입니다")
            .setContentText("${ticket.title} · ${ticket.locationFrom} → ${ticket.locationTo}")
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setCategory(NotificationCompat.CATEGORY_REMINDER)
            .setAutoCancel(true)
            .setContentIntent(contentIntent)
            .build()

        manager.notify(ticket.id.toInt(), notification)
    }
}
