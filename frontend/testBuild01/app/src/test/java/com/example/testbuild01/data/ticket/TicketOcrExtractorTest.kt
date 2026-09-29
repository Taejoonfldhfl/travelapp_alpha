package com.example.testbuild01.data.ticket

import com.example.testbuild01.data.local.TicketType
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.Calendar

class TicketOcrExtractorTest {

    @Test
    fun extractTicketFields_readsFlightNumberRouteAndDateTime() {
        val text = """
            대한항공 KE901
            출발: 인천 2026-10-05 09:20
            도착: 뉴욕
            예약번호: ABCDE1
        """.trimIndent()

        val candidate = extractTicketFields(text)

        assertEquals(TicketType.FLIGHT, candidate.type)
        assertEquals("KE901", candidate.title)
        assertEquals("인천", candidate.locationFrom)
        assertEquals("뉴욕", candidate.locationTo)
        assertEquals("ABCDE1", candidate.confirmationNumber)
        assertTrue(candidate.startDateTimeMillis != null)

        val cal = Calendar.getInstance().apply { timeInMillis = candidate.startDateTimeMillis!! }
        assertEquals(2026, cal.get(Calendar.YEAR))
        assertEquals(Calendar.OCTOBER, cal.get(Calendar.MONTH))
        assertEquals(5, cal.get(Calendar.DAY_OF_MONTH))
        assertEquals(9, cal.get(Calendar.HOUR_OF_DAY))
        assertEquals(20, cal.get(Calendar.MINUTE))
    }

    @Test
    fun extractTicketFields_busTicketWithRouteArrow_detectsBusTypeAndRoute() {
        val text = """
            고속버스 승차권
            서울 → 부산
            2026-11-01
            예약번호: BUS999
        """.trimIndent()

        val candidate = extractTicketFields(text)

        assertEquals(TicketType.BUS, candidate.type)
        assertEquals("서울", candidate.locationFrom)
        assertEquals("부산", candidate.locationTo)
        assertEquals("서울 → 부산", candidate.title)
        assertEquals("BUS999", candidate.confirmationNumber)
        assertTrue(candidate.startDateTimeMillis != null)
    }

    @Test
    fun extractTicketFields_dateWithoutTime_fillsNoonAsPlaceholder() {
        val candidate = extractTicketFields("출발: 2026-12-25")

        assertTrue(candidate.startDateTimeMillis != null)
        val cal = Calendar.getInstance().apply { timeInMillis = candidate.startDateTimeMillis!! }
        assertEquals(12, cal.get(Calendar.HOUR_OF_DAY))
        assertEquals(0, cal.get(Calendar.MINUTE))
    }

    @Test
    fun extractTicketFields_onUnrecognizableText_returnsBlankCandidateWithDefaultType() {
        val candidate = extractTicketFields("아무 의미 없는 스캔 결과")

        assertEquals(TicketType.FLIGHT, candidate.type)
        assertEquals("", candidate.title)
        assertEquals("", candidate.locationFrom)
        assertEquals("", candidate.locationTo)
        assertEquals("", candidate.confirmationNumber)
        assertNull(candidate.startDateTimeMillis)
    }
}
