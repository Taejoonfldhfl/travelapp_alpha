package com.example.testbuild01.data.ticket

import com.example.testbuild01.data.local.TicketType
import java.text.SimpleDateFormat
import java.util.Calendar
import java.util.Locale

/** OCR 원문 텍스트에서 뽑아낸 추정값. 사용자가 검토 화면(TicketManualEntryScreen)에서 확인/수정하기 전까지는 저장되지 않는다. */
data class TicketOcrCandidate(
    val type: TicketType = TicketType.FLIGHT,
    val title: String = "",
    val startDateTimeMillis: Long? = null,
    val locationFrom: String = "",
    val locationTo: String = "",
    val confirmationNumber: String = ""
)

// 항공편명: 항공사 코드(영문 2자 또는 숫자+영문 1자, 예: KE/OZ/7C) + 2~4자리 편명 번호.
private val FLIGHT_NUMBER_REGEX = Regex("""\b([A-Z]{2}\d{2,4}|\d[A-Z]\d{2,4})\b""")
private val BUS_KEYWORD_REGEX = Regex("""고속버스|시외버스|버스승차권""")

private val CONFIRMATION_REGEX = Regex(
    """(?:예약번호|확인번호|예약확인번호|PNR|Booking(?:\s*(?:No\.?|Reference|Number))?|Confirmation(?:\s*(?:No\.?|Number))?)[:#\s]+([A-Za-z0-9-]{4,20})""",
    RegexOption.IGNORE_CASE
)

// "출발: 2026-10-01 14:30" 처럼 라벨 뒤에 날짜(+선택적으로 시각)가 붙는 형태를 우선 찾는다.
private val DEPARTURE_DATE_TIME_REGEX = Regex(
    """(?:출발(?:일시|시간)?|Departure)[^0-9]{0,10}(\d{4}[-./]\d{1,2}[-./]\d{1,2})(?:[^0-9]{0,5}(\d{1,2}:\d{2}))?""",
    RegexOption.IGNORE_CASE
)
// 라벨을 못 찾으면, 문서 어디든 처음 나오는 "날짜 + 시각" 조합을 출발 일시로 대신 쓴다.
private val ANY_DATE_TIME_REGEX = Regex("""(\d{4}[-./]\d{1,2}[-./]\d{1,2})[^0-9]{0,5}(\d{1,2}:\d{2})""")
// 시각도 라벨도 없이 날짜만 적힌 티켓(버스표 등)을 위한 최후 fallback.
private val ANY_DATE_REGEX = Regex("""(\d{4}[-./]\d{1,2}[-./]\d{1,2})""")

private val DEPARTURE_PLACE_REGEX = Regex(
    """(?:출발(?:지)?|Departure|From)[:\s]+([가-힣A-Za-z0-9()]{2,20})""",
    RegexOption.IGNORE_CASE
)
private val ARRIVAL_PLACE_REGEX = Regex(
    """(?:도착(?:지)?|Arrival|To)[:\s]+([가-힣A-Za-z0-9()]{2,20})""",
    RegexOption.IGNORE_CASE
)
// 라벨이 없는 티켓은 "서울 → 부산", "서울-부산"처럼 구간을 화살표/하이픈으로 적는 경우가 많아 그걸 대신 찾는다.
private val ROUTE_ARROW_REGEX = Regex("""([가-힣A-Za-z]{2,20})\s*(?:→|->|~)\s*([가-힣A-Za-z]{2,20})""")

/** 항공권/버스표 OCR 원문에서 type/title/startDateTime/locationFrom/locationTo/confirmationNumber 후보값을 추출한다. */
fun extractTicketFields(rawText: String): TicketOcrCandidate {
    val flightMatch = FLIGHT_NUMBER_REGEX.find(rawText)?.groupValues?.get(1)
    val type = when {
        flightMatch != null -> TicketType.FLIGHT
        BUS_KEYWORD_REGEX.containsMatchIn(rawText) -> TicketType.BUS
        else -> TicketType.FLIGHT
    }

    val from = DEPARTURE_PLACE_REGEX.find(rawText)?.groupValues?.get(1)?.trim().orEmpty()
    val to = ARRIVAL_PLACE_REGEX.find(rawText)?.groupValues?.get(1)?.trim().orEmpty()
    val (routeFrom, routeTo) = if (from.isBlank() || to.isBlank()) {
        ROUTE_ARROW_REGEX.find(rawText)?.let { it.groupValues[1].trim() to it.groupValues[2].trim() }
            ?: ("" to "")
    } else {
        "" to ""
    }
    val resolvedFrom = from.ifBlank { routeFrom }
    val resolvedTo = to.ifBlank { routeTo }

    return TicketOcrCandidate(
        type = type,
        title = when {
            flightMatch != null -> flightMatch
            resolvedFrom.isNotBlank() && resolvedTo.isNotBlank() -> "$resolvedFrom → $resolvedTo"
            else -> ""
        },
        startDateTimeMillis = extractStartDateTime(rawText),
        locationFrom = resolvedFrom,
        locationTo = resolvedTo,
        confirmationNumber = CONFIRMATION_REGEX.find(rawText)?.groupValues?.get(1).orEmpty()
    )
}

private fun extractStartDateTime(rawText: String): Long? {
    DEPARTURE_DATE_TIME_REGEX.find(rawText)?.let { match ->
        val timePart = match.groupValues.getOrNull(2)?.takeIf { it.isNotBlank() }
        return parseDateTime(match.groupValues[1], timePart)
    }
    ANY_DATE_TIME_REGEX.find(rawText)?.let { match ->
        return parseDateTime(match.groupValues[1], match.groupValues[2])
    }
    ANY_DATE_REGEX.find(rawText)?.let { match ->
        return parseDateTime(match.groupValues[1], null)
    }
    return null
}

private fun parseDateTime(datePart: String, timePart: String?): Long? {
    val normalizedDate = datePart.replace('.', '-').replace('/', '-')
    return runCatching {
        val pattern = if (timePart != null) "yyyy-MM-dd HH:mm" else "yyyy-MM-dd"
        val format = SimpleDateFormat(pattern, Locale.KOREA)
        format.isLenient = false
        val text = if (timePart != null) "$normalizedDate $timePart" else normalizedDate
        val date = format.parse(text) ?: return null
        if (timePart != null) {
            date.time
        } else {
            // 시각을 못 찾으면 정오로 채워 넣는다(사용자가 검토 화면에서 바로 잡을 수 있게 명백히 임시값으로).
            Calendar.getInstance().apply {
                time = date
                set(Calendar.HOUR_OF_DAY, 12)
                set(Calendar.MINUTE, 0)
                set(Calendar.SECOND, 0)
                set(Calendar.MILLISECOND, 0)
            }.timeInMillis
        }
    }.getOrNull()
}
