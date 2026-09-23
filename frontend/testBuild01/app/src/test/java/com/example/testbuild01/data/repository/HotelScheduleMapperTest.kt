package com.example.testbuild01.data.repository

import com.example.testbuild01.data.local.HotelDetail
import com.example.testbuild01.data.model.ScheduleResponse
import com.google.gson.Gson
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.TimeZone

class HotelScheduleMapperTest {

    private val utc = TimeZone.getTimeZone("UTC")

    private val hotel = HotelDetail(
        hotelName = "H".repeat(80),
        address = "A".repeat(150),
        latitude = null,
        longitude = null,
        checkInTime = 1_790_953_200_000L,
        checkOutTime = 1_791_111_600_000L,
        roomType = null,
        guestCount = 1,
        confirmationNumber = "CONF-SECRET-777",
        guestNameOnBooking = "HONG GILDONG",
        freeCancellationDeadline = null,
        phoneNumber = "010-0000-0000"
    )

    @Test
    fun createRequest_neverContainsPersonalBookingInfo() {
        val json = Gson().toJson(HotelScheduleMapper.toCreateRequest(hotel, utc))

        assertFalse(json.contains("CONF-SECRET-777"))
        assertFalse(json.contains("HONG GILDONG"))
        assertFalse(json.contains("010-0000-0000"))
    }

    @Test
    fun createRequest_truncatesToServerLimits() {
        val request = HotelScheduleMapper.toCreateRequest(hotel, utc)

        assertEquals(50, request.title.length)
        assertEquals(100, request.placeName.length)
    }

    @Test
    fun updateRequest_withoutLocalCoordinates_keepsServerCoordinates() {
        val current = ScheduleResponse(
            id = 1, tripId = 7, title = "", placeName = "", description = "메모", startTime = "", endTime = "",
            order = 2, latitude = 35.0, longitude = 129.0, priority = 1, isEssential = false,
            isHotelCheckIn = true, createdAt = ""
        )

        val request = HotelScheduleMapper.toUpdateRequest(current, hotel, utc)

        assertEquals(35.0, request.latitude!!, 0.0)
        assertEquals(129.0, request.longitude!!, 0.0)
        assertEquals(1, request.priority)
        assertFalse(request.isEssential)
        assertTrue(request.isHotelCheckIn)
    }

    @Test
    fun isSameHotelSchedule_ignoresServerFractionalSeconds() {
        val request = HotelScheduleMapper.toCreateRequest(hotel, utc)
        val server = ScheduleResponse(
            id = 1, tripId = 7, title = request.title, placeName = request.placeName, description = "",
            startTime = request.startTime + ".0000000", endTime = request.endTime, order = 0,
            latitude = null, longitude = null, priority = 0, isEssential = true, isHotelCheckIn = true, createdAt = ""
        )

        assertTrue(HotelScheduleMapper.isSameHotelSchedule(server, request))
        assertFalse(HotelScheduleMapper.isSameHotelSchedule(server.copy(isHotelCheckIn = false), request))
        assertFalse(HotelScheduleMapper.isSameHotelSchedule(server.copy(endTime = "2026-10-05T11:00:00"), request))
    }

    @Test
    fun scheduleFieldsChanged_ignoresPrivateOnlyEdits() {
        val edited = hotel.copy(confirmationNumber = "NEW", roomType = "스위트", guestCount = 3)

        assertFalse(HotelScheduleMapper.scheduleFieldsChanged(hotel, edited))
        assertTrue(HotelScheduleMapper.scheduleFieldsChanged(hotel, hotel.copy(checkOutTime = hotel.checkOutTime + 1)))
    }
}
