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
