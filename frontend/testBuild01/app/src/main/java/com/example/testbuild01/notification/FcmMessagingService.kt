package com.example.testbuild01.notification

import android.annotation.SuppressLint
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.example.testbuild01.MainActivity
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import kotlin.random.Random

// 서버(TravelApp.WebAPI)가 FCM으로 보내는 여행 관련 알림(예산 초과, 정산 결과 도착 등)을 받는다.
// 서버는 제목/본문만 있는 알림 메시지를 보내므로, 종류를 구분하지 않고 하나의 채널로 표시한다.
class FcmMessagingService : FirebaseMessagingService() {
    companion object {
        const val CHANNEL_ID = "trip_push"
    }

    override fun onNewToken(token: String) {
        super.onNewToken(token)
        FcmTokenRegistrar.sendToServer(token)
    }

    @SuppressLint("MissingPermission")
    override fun onMessageReceived(message: RemoteMessage) {
        super.onMessageReceived(message)
        val notification = message.notification ?: return

        val manager = NotificationManagerCompat.from(this)
        if (!manager.areNotificationsEnabled()) return
        createChannel()

        val contentIntent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
            },
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val builder = NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_dialog_info)
            .setContentTitle(notification.title ?: "여행 알림")
            .setContentText(notification.body ?: "")
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setContentIntent(contentIntent)
            .setAutoCancel(true)
            .build()

        manager.notify(Random.nextInt(), builder)
    }

    private fun createChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val channel = NotificationChannel(
            CHANNEL_ID,
            "여행 알림",
            NotificationManager.IMPORTANCE_HIGH
        ).apply { description = "예산 초과, 정산 결과 등 다른 멤버에게서 오는 알림" }
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }
}
