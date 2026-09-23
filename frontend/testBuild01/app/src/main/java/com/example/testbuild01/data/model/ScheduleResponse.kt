package com.example.testbuild01.data.model

data class ScheduleResponse(
    val id: Int,
    val tripId: Int,
    val title: String,
    val placeName: String,
    val description: String,
    val startTime: String,
    val endTime: String,
    val order: Int,
    val latitude: Double?,
    val longitude: Double?,
    val priority: Int,
    val isEssential: Boolean,
    val isHotelCheckIn: Boolean = false,
    val createdAt: String
)
