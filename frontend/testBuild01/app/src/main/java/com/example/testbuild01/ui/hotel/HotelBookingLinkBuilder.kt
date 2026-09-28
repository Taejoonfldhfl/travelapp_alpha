package com.example.testbuild01.ui.hotel

import java.net.URLEncoder
import java.nio.charset.StandardCharsets

/**
 * 숙소 이름으로 외부 예약 사이트의 "일반 검색 페이지" URL을 만든다(순수 함수, android.* 의존 없음).
 *
 * 임시 방식: 제휴(affiliate) 딥링크가 아니라 각 사이트의 공개 검색 페이지로 보내는 것뿐이다. 이 앱은 가격·잔여객실·
 * 예약·결제에 관여하지 않으며, 정식 제휴 API(아고다/트립닷컴 파트너)가 붙으면 이 빌더를 제휴 링크 생성으로 교체한다.
 * 또한 각 사이트가 검색 파라미터 이름(text, keyword)을 바꾸면 검색어가 적용되지 않을 수 있다.
 */
object HotelBookingLinkBuilder {

    enum class Site(val label: String) {
        AGODA("아고다"),
        TRIP_COM("트립닷컴")
    }

    fun searchUrl(site: Site, hotelName: String): String {
        val query = encodeQueryValue(normalizeName(hotelName))
        return when (site) {
            Site.AGODA -> "https://www.agoda.com/search?text=$query"
            Site.TRIP_COM -> "https://www.trip.com/hotels/list?keyword=$query"
        }
    }

    // 앞뒤 공백을 지우고, 줄바꿈/탭/연속 공백을 공백 하나로 줄인다("호텔OO  서울점" -> "호텔OO 서울점").
    internal fun normalizeName(hotelName: String): String =
        hotelName.trim().replace(Regex("\\s+"), " ")

    // 쿼리 값 인코딩(UTF-8 퍼센트 인코딩). URLEncoder는 폼 인코딩이라 공백을 '+'로 바꾸는데, '+'는 사이트에 따라
    // 문자 그대로 해석될 수 있어 %20으로 바꾼다. 원래 '+' 문자는 URLEncoder가 이미 %2B로 바꿔 두므로 섞이지 않는다.
    internal fun encodeQueryValue(value: String): String =
        URLEncoder.encode(value, StandardCharsets.UTF_8.name())
            .replace("+", "%20")
            .replace("*", "%2A")
}
