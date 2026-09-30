package com.example.testbuild01.notification

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import com.example.testbuild01.MainApplication
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

/**
 * 재부팅 시 AlarmManager 예약이 사라지므로 미래 티켓을 다시 등록한다.
 * 정확한 알람 권한 상태가 바뀔 때도 호출되어 폴백 → 정확한 알람으로 승격된다.
 */
class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        // AndroidManifest에 등록된 두 protected broadcast만 허용한다(스푸핑 방지). 상수
        // AlarmManager.ACTION_SCHEDULE_EXACT_ALARM_PERMISSION_STATE_CHANGED는 API 34+라
        // minSdk(24)와 맞추기 위해 매니페스트와 같은 문자열을 그대로 쓴다.
        val allowedActions = setOf(
            Intent.ACTION_BOOT_COMPLETED,
            "android.app.action.SCHEDULE_EXACT_ALARM_PERMISSION_STATE_CHANGED"
        )
        if (intent.action !in allowedActions) return

        val pending = goAsync()
        CoroutineScope(Dispatchers.IO).launch {
            try {
                (context.applicationContext as MainApplication).ticketRepository.rescheduleUpcoming()
            } finally {
                pending.finish()
            }
        }
    }
}
