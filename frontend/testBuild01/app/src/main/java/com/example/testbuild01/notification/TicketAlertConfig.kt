package com.example.testbuild01.notification

import com.example.testbuild01.data.local.TicketType

object TicketAlertConfig {
    const val CHANNEL_ID = "ticket_alerts"

    /** 탑승/승차 몇 분 전에 알릴지 (빠른 기능 테스트를 위해 10분) */
    const val LEAD_TIME_MINUTES = 10L

    fun alertTimeMillis(startDateTime: Long): Long = startDateTime - LEAD_TIME_MINUTES * 60 * 1000

    fun verbFor(type: TicketType): String = when (type) {
        TicketType.BUS -> "승차"
        TicketType.FLIGHT -> "탑승"
    }
}
