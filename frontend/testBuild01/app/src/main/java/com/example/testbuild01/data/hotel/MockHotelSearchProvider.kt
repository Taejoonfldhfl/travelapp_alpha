package com.example.testbuild01.data.hotel

/** Amadeus 연동 전까지 검색 리스트 화면 테스트용으로 고정 템플릿 호텔을 반환한다. */
class MockHotelSearchProvider : IHotelSearchProvider {
    override suspend fun searchHotels(query: String, checkIn: Long, checkOut: Long): List<HotelSearchResult> =
        TEMPLATE_HOTELS

    private companion object {
        val TEMPLATE_HOTELS = listOf(
            HotelSearchResult(
                hotelName = "서울 스퀘어 호텔",
                address = "서울 중구 세종대로 123",
                latitude = 37.5665,
                longitude = 126.9780,
                priceRangeLabel = "1박 12~15만원"
            ),
            HotelSearchResult(
                hotelName = "명동 그랜드 호텔",
                address = "서울 중구 명동길 45",
                latitude = 37.5636,
                longitude = 126.9834,
                priceRangeLabel = "1박 10~13만원"
            ),
            HotelSearchResult(
                hotelName = "홍대 부티크 호텔",
                address = "서울 마포구 양화로 200",
                latitude = 37.5563,
                longitude = 126.9236,
                priceRangeLabel = "1박 8~11만원"
            ),
            HotelSearchResult(
                hotelName = "강남 비즈니스 호텔",
                address = "서울 강남구 테헤란로 500",
                latitude = 37.5006,
                longitude = 127.0364,
                priceRangeLabel = "1박 13~18만원"
            ),
            HotelSearchResult(
                hotelName = "부산 해운대 오션 호텔",
                address = "부산 해운대구 해운대해변로 30",
                latitude = 35.1587,
                longitude = 129.1604,
                priceRangeLabel = "1박 11~16만원"
            ),
            HotelSearchResult(
                hotelName = "제주 서귀포 리조트",
                address = "제주 서귀포시 중문관광로 80",
                latitude = 33.2489,
                longitude = 126.4116,
                priceRangeLabel = "1박 15~22만원"
            )
        )
    }
}
