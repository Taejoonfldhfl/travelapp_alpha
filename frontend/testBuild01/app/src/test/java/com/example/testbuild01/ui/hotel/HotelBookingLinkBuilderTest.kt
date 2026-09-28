package com.example.testbuild01.ui.hotel

import com.example.testbuild01.ui.hotel.HotelBookingLinkBuilder.Site
import java.net.URI
import java.net.URLDecoder
import java.nio.charset.StandardCharsets
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class HotelBookingLinkBuilderTest {

    // URL에서 검색어 파라미터를 꺼내 디코딩한다(인코딩이 되돌려지는지 = 깨지지 않았는지 확인).
    private fun decodedQuery(url: String, param: String): String {
        val rawQuery = URI(url).rawQuery
        val value = rawQuery.split("&").first { it.startsWith("$param=") }.substringAfter("=")
        return URLDecoder.decode(value, StandardCharsets.UTF_8.name())
    }

    @Test
    fun agoda_usesSearchPageWithTextParam() {
        assertEquals(
            "https://www.agoda.com/search?text=%ED%98%B8%ED%85%94",
            HotelBookingLinkBuilder.searchUrl(Site.AGODA, "호텔")
        )
    }

    @Test
    fun tripCom_usesHotelListPageWithKeywordParam() {
        assertEquals(
            "https://www.trip.com/hotels/list?keyword=%ED%98%B8%ED%85%94",
            HotelBookingLinkBuilder.searchUrl(Site.TRIP_COM, "호텔")
        )
    }

    @Test
    fun branchNameWithSpace_isEncodedAsPercent20_notPlus() {
        val url = HotelBookingLinkBuilder.searchUrl(Site.AGODA, "호텔OO 서울점")

        assertTrue(url.endsWith("%ED%98%B8%ED%85%94OO%20%EC%84%9C%EC%9A%B8%EC%A0%90"))
        assertFalse(url.contains("+"))
        assertFalse(url.contains(" "))
        assertEquals("호텔OO 서울점", decodedQuery(url, "text"))
    }

    @Test
    fun specialCharacters_areEncoded_andRoundTrip() {
        val names = listOf(
            "호텔 & 리조트",
            "A+B 호텔",
            "호텔#1 (본관)",
            "Stay?Here=Yes/No",
            "L'Hôtel 서울 100%",
            "호텔*스타 [신관]",
            "스테이, 강남·역삼"
        )

        for (name in names) {
            for (site in Site.values()) {
                val url = HotelBookingLinkBuilder.searchUrl(site, name)
                val param = if (site == Site.AGODA) "text" else "keyword"

                URI(url) // 형식이 잘못된 URL이면 예외
                assertEquals("$site / $name", name, decodedQuery(url, param))
                // 검색어가 다른 파라미터나 경로로 새지 않는다.
                assertEquals("$site / $name", 1, URI(url).rawQuery.split("&").size)
                assertFalse("$site / $name", URI(url).rawQuery.contains("#"))
            }
        }
    }

    @Test
    fun plusSign_inName_isKeptAsLiteralPlus() {
        val url = HotelBookingLinkBuilder.searchUrl(Site.TRIP_COM, "A+B 호텔")

        assertTrue(url.contains("A%2BB%20"))
    }

    @Test
    fun whitespace_isTrimmedAndCollapsed() {
        val url = HotelBookingLinkBuilder.searchUrl(Site.AGODA, "  호텔OO \n  서울점\t")

        assertEquals("호텔OO 서울점", decodedQuery(url, "text"))
    }

    @Test
    fun englishName_isEncodedToo() {
        assertEquals(
            "https://www.trip.com/hotels/list?keyword=Four%20Seasons%20Hotel%20Seoul",
            HotelBookingLinkBuilder.searchUrl(Site.TRIP_COM, "Four Seasons Hotel Seoul")
        )
    }
}
