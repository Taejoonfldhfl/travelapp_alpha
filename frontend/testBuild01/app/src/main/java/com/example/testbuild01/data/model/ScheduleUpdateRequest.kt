package com.example.testbuild01.data.model

/**
 * 서버 PUT은 모든 필드를 덮어쓴다. 좌표·우선순위·체크인 플래그를 빼고 보내면 서버 값이 null/기본값으로 초기화되므로,
 * 일부만 바꾸려면 현재 일정을 먼저 조회해 나머지 값을 그대로 채워 보내야 한다.
 */
data class ScheduleUpdateRequest(
    val title: String,
    val placeName: String,
    val description: String,
    val startTime: String,
    val endTime: String,
    val order: Int,
    val latitude: Double? = null,
    val longitude: Double? = null,
    val priority: Int = 0,
    val isEssential: Boolean = true,
    val isHotelCheckIn: Boolean = false
)
