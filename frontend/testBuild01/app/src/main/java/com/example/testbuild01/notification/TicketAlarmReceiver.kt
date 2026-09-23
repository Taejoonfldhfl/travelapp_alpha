package com.example.testbuild01.notification

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import com.example.testbuild01.MainApplication
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

class TicketAlarmReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val ticketId = intent.getLongExtra(EXTRA_TICKET_ID, -1L)
        if (ticketId < 0) return
        val slot = intent.getStringExtra(EXTRA_SLOT)?.let { runCatching { AlarmSlot.valueOf(it) }.getOrNull() }
            ?: AlarmSlot.PRIMARY

        val pending = goAsync()
        CoroutineScope(Dispatchers.IO).launch {
            try {
                // 알림 시점의 최신 데이터를 사용하고, 그 사이 삭제된 티켓이면 알리지 않는다.
                val repository = (context.applicationContext as MainApplication).ticketRepository
                repository.getById(ticketId)?.let { TicketNotifier.show(context, it, slot) }
            } finally {
                pending.finish()
            }
        }
    }

    companion object {
        const val ACTION_TICKET_ALARM = "com.example.testbuild01.ACTION_TICKET_ALARM"
        const val EXTRA_TICKET_ID = "ticket_id"
        const val EXTRA_SLOT = "alarm_slot"
    }
}
