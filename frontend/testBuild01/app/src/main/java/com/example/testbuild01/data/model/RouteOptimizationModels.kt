package com.example.testbuild01.data.model

data class RouteOptimizationStop(
    val scheduleId: Int,
    val title: String,
    val placeName: String,
    val latitude: Double,
    val longitude: Double,
    val visitOrder: Int,
    val travelTimeFromPreviousSeconds: Double,
    val isHotelCheckIn: Boolean = false
)

data class RouteOptimizationResult(
    val date: String,
    val stops: List<RouteOptimizationStop>,
    val totalTravelTimeSeconds: Double,
    val solverUsed: String,
    val applied: Boolean,
    val skippedSchedulesWithoutCoordinates: List<String>,
    val anchorAdjusted: Boolean = false
)
