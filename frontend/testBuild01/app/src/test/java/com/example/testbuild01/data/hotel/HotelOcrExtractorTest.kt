package com.example.testbuild01.data.hotel

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.Calendar

class HotelOcrExtractorTest {

    @Test
    fun extractHotelFields_readsConfirmationNumberAndDates() {
        val text = """
            서울 스퀘어 호텔
            주소: 서울 중구 세종대로 123
            체크인: 2026-10-01
            체크아웃: 2026-10-03
            예약번호: ABC-123456
            예약자: 홍길동
        """.trimIndent()

        val candidate = extractHotelFields(text)

        assertEquals("서울 스퀘어 호텔", candidate.hotelName)
        assertEquals("서울 중구 세종대로 123", candidate.address)
        assertEquals("ABC-123456", candidate.confirmationNumber)
        assertEquals("홍길동", candidate.guestNameOnBooking)
        assertTrue(candidate.checkInMillis != null)
        assertTrue(candidate.checkOutMillis != null)

        val checkIn = Calendar.getInstance().apply { timeInMillis = candidate.checkInMillis!! }
        assertEquals(15, checkIn.get(Calendar.HOUR_OF_DAY))
        val checkOut = Calendar.getInstance().apply { timeInMillis = candidate.checkOutMillis!! }
        assertEquals(11, checkOut.get(Calendar.HOUR_OF_DAY))
    }

    @Test
    fun extractHotelFields_onUnrecognizableText_returnsBlankCandidate() {
        val candidate = extractHotelFields("아무 의미 없는 스캔 결과")

        assertEquals("", candidate.confirmationNumber)
        assertEquals(null, candidate.checkInMillis)
        assertEquals(null, candidate.checkOutMillis)
    }
}
