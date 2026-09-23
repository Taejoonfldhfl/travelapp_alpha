package com.example.testbuild01.data.local

/**
 * 호텔 예약 상세 정보. [TicketDetails] 안에 중첩되어 함께 암호화된다.
 * confirmationNumber/guestNameOnBooking은 체크인 시 프런트 대조에 실제로 쓰이는 핵심 필드라
 * 구조상 별도 필드로 두어 UI에서 강조 표시할 수 있게 한다.
 */
data class HotelDetail(
    val hotelName: String,
    val address: String,
    val latitude: Double?,
    val longitude: Double?,
    val checkInTime: Long,
    val checkOutTime: Long,
    val roomType: String?,
    val guestCount: Int,
    val confirmationNumber: String,
    val guestNameOnBooking: String,
    val freeCancellationDeadline: Long?,
    val phoneNumber: String?
)
