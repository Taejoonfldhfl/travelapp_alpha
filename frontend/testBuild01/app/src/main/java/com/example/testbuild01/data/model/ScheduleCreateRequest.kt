package com.example.testbuild01.data.model

data class ScheduleCreateRequest(
    val title: String,
    val placeName: String,
    val description: String,
    val startTime: String,
    val endTime: String,
    val order: Int
)