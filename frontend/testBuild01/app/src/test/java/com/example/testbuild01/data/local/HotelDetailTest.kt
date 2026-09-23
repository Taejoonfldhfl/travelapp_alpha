package com.example.testbuild01.data.local

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class HotelDetailTest {

    private fun sampleHotel() = HotelDetail(
        hotelName = "서울 스퀘어 호텔",
        address = "서울 중구 세종대로 123",
        latitude = 37.5665,
        longitude = 126.9780,
        checkInTime = 1_700_000_000_000L,
        checkOutTime = 1_700_100_000_000L,
        roomType = "디럭스 트윈",
        guestCount = 2,
        confirmationNumber = "ABC-123456",
        guestNameOnBooking = "홍길동",
        freeCancellationDeadline = 1_699_900_000_000L,
        phoneNumber = "02-1234-5678"
    )

    @Test
    fun hotelDetail_roundTripsThroughJson() {
        val details = TicketDetails(
            confirmationNumber = "ABC-123456",
            barcodeValue = "",
            barcodeFormat = "",
            hotel = sampleHotel()
        )

        val restored = details.toJson().toTicketDetails()

        assertEquals(details, restored)
        assertEquals(sampleHotel(), restored.hotel)
    }

    @Test
    fun legacyBusOrFlightJson_withoutHotelKey_decodesWithNullHotel() {
        // 호텔 기능 추가 전에 저장된 BUS/FLIGHT용 JSON 형태를 그대로 재현한다.
        val legacyJson = """{"confirmationNumber":"XYZ-999","barcodeValue":"QR-1","barcodeFormat":"QR_CODE"}"""

        val restored = legacyJson.toTicketDetails()

        assertEquals("XYZ-999", restored.confirmationNumber)
        assertEquals("QR-1", restored.barcodeValue)
        assertNull(restored.hotel)
    }
}
