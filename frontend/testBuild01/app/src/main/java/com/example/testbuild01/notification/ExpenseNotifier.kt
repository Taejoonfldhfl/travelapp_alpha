package com.example.testbuild01.notification

import android.annotation.SuppressLint
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.example.testbuild01.MainActivity

// 지출 등록/수정 직후 예산 초과가 확인되면 로컬 알림을 띄운다.
object ExpenseNotifier {
    const val CHANNEL_ID = "expense_budget_alert"

    fun createChannel(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val channel = NotificationChannel(
            CHANNEL_ID,
            "예산 알림",
            NotificationManager.IMPORTANCE_HIGH
        ).apply { description = "여행 예산 초과 알림" }
        context.getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }

    @SuppressLint("MissingPermission")
    fun showBudgetExceeded(context: Context, tripId: Int, overAmount: Double) {
        val manager = NotificationManagerCompat.from(context)
        // POST_NOTIFICATIONS 거부 또는 알림 차단 상태면 조용히 건너뛴다.
        if (!manager.areNotificationsEnabled()) return
        createChannel(context)

        val contentIntent = PendingIntent.getActivity(
            context,
            tripId,
            Intent(context, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
            },
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val notification = NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_dialog_alert)
            .setContentTitle("예산 초과")
            .setContentText("여행 예산을 ${"%,d".format(overAmount.toLong())}원 초과했어요.")
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setContentIntent(contentIntent)
            .setAutoCancel(true)
            .build()

        manager.notify(NOTIFICATION_ID_BASE + tripId, notification)
    }

    private const val NOTIFICATION_ID_BASE = 40_000
}

// 다른 멤버의 지출로 예산이 초과된 경우는 서버발 푸시(FCM)가 필요하다.
// 실제 FCM 연동은 미루고, 인터페이스와 로그만 남기는 Mock 만 둔다.
interface IPushNotifier {
    fun onRemoteBudgetExceeded(tripId: Int, overAmount: Double)
}

class LogPushNotifier : IPushNotifier {
    override fun onRemoteBudgetExceeded(tripId: Int, overAmount: Double) {
        Log.d("MockPush", "trip=$tripId budget exceeded by $overAmount (FCM 미연동)")
    }
}
