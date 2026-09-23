package com.example.testbuild01.data.hotel

import java.text.SimpleDateFormat
import java.util.Calendar
import java.util.Locale

/** OCR 원문 텍스트에서 뽑아낸 추정값. 사용자가 검토 화면에서 확인/수정하기 전까지는 저장되지 않는다. */
data class HotelOcrCandidate(
    val hotelName: String = "",
    val address: String = "",
    val checkInMillis: Long? = null,
    val checkOutMillis: Long? = null,
    val confirmationNumber: String = "",
    val guestNameOnBooking: String = ""
)

private val CONFIRMATION_REGEX = Regex(
    """(?:확인번호|예약번호|예약확인번호|Confirmation(?:\s*(?:No\.?|Number))?|Booking\s*ID)[:#\s]+([A-Za-z0-9-]{4,20})""",
    RegexOption.IGNORE_CASE
)
private val CHECK_IN_REGEX = Regex(
    """(?:체크인|Check[- ]?in)[^0-9]{0,10}(\d{4}[-./]\d{1,2}[-./]\d{1,2})""",
    RegexOption.IGNORE_CASE
)
private val CHECK_OUT_REGEX = Regex(
    """(?:체크아웃|Check[- ]?out)[^0-9]{0,10}(\d{4}[-./]\d{1,2}[-./]\d{1,2})""",
    RegexOption.IGNORE_CASE
)
private val GUEST_NAME_REGEX = Regex(
    """(?:예약자|투숙객|Guest(?:\s*Name)?)[:\s]+([A-Za-z가-힣\s]{2,20})""",
    RegexOption.IGNORE_CASE
)
private val ADDRESS_REGEX = Regex(
    """(?:주소|Address)[:\s]+(.{5,60})""",
    RegexOption.IGNORE_CASE
)

private const val DEFAULT_CHECK_IN_HOUR = 15
private const val DEFAULT_CHECK_OUT_HOUR = 11

/** 호텔 확인서 OCR 원문에서 hotelName/checkInDate/checkOutDate/confirmationNumber 등 후보값을 추출한다. */
fun extractHotelFields(rawText: String): HotelOcrCandidate {
    val lines = rawText.lines().map { it.trim() }.filter { it.isNotEmpty() }

    return HotelOcrCandidate(
        // 호텔명은 보통 확인서 맨 위에 나오므로 숫자 없는 첫 줄을 추정값으로 쓴다. 검토 화면에서 사용자가 고친다.
        hotelName = lines.firstOrNull { line -> line.length in 2..40 && line.none { it.isDigit() } }.orEmpty(),
        address = ADDRESS_REGEX.find(rawText)?.groupValues?.get(1)?.trim().orEmpty(),
        checkInMillis = CHECK_IN_REGEX.find(rawText)?.groupValues?.get(1)?.let { parseDate(it, DEFAULT_CHECK_IN_HOUR) },
        checkOutMillis = CHECK_OUT_REGEX.find(rawText)?.groupValues?.get(1)?.let { parseDate(it, DEFAULT_CHECK_OUT_HOUR) },
        confirmationNumber = CONFIRMATION_REGEX.find(rawText)?.groupValues?.get(1).orEmpty(),
        guestNameOnBooking = GUEST_NAME_REGEX.find(rawText)?.groupValues?.get(1)?.trim().orEmpty()
    )
}

// 체크인/체크아웃 시각까지는 문서에 없는 경우가 많아 일반적인 호텔 체크인(15시)/체크아웃(11시) 기본값을 붙인다.
private fun parseDate(text: String, hour: Int): Long? {
    val normalized = text.replace('.', '-').replace('/', '-')
    return runCatching {
        val format = SimpleDateFormat("yyyy-MM-dd", Locale.KOREA)
        format.isLenient = false
        val date = format.parse(normalized) ?: return null
        Calendar.getInstance().apply {
            time = date
            set(Calendar.HOUR_OF_DAY, hour)
            set(Calendar.MINUTE, 0)
            set(Calendar.SECOND, 0)
            set(Calendar.MILLISECOND, 0)
        }.timeInMillis
    }.getOrNull()
}
