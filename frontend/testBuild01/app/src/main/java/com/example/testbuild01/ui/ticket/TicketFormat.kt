package com.example.testbuild01.ui.ticket

import com.example.testbuild01.data.local.TicketType
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

fun formatDateTime(millis: Long): String =
    SimpleDateFormat("yyyy-MM-dd HH:mm", Locale.KOREA).format(Date(millis))

fun TicketType.label(): String = when (this) {
    TicketType.BUS -> "버스"
    TicketType.FLIGHT -> "항공"
}
