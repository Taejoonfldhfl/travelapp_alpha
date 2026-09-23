package com.example.testbuild01.data.hotel

/** 검색 결과 리스트 화면에 표시할 호텔 요약 정보. */
data class HotelSearchResult(
    val hotelName: String,
    val address: String,
    val latitude: Double,
    val longitude: Double,
    val priceRangeLabel: String
)

/**
 * 호텔 검색/비교 제공자. Amadeus 호텔 검색 API 키를 발급받기 전까지는
 * [MockHotelSearchProvider]만 존재하며, 실제 연동은 이 인터페이스 뒤에 새 구현체를 추가해서 진행한다.
 */
interface IHotelSearchProvider {
    suspend fun searchHotels(query: String, checkIn: Long, checkOut: Long): List<HotelSearchResult>
}
