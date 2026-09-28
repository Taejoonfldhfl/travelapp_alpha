package com.example.testbuild01.data.model

// 서버 GET api/HotelInfo/search 응답 항목(TourAPI 숙박시설 정보). 정보 조회 전용이라 가격/잔여객실은 없다.
// latitude/longitude는 서버가 해석하지 못했으면 null — 목록에는 표시하고 지도 마커만 생략한다.
data class HotelInfoItem(
    val contentId: String,
    val name: String,
    val address: String = "",
    val tel: String? = null,
    val imageUrl: String? = null,
    val latitude: Double? = null,
    val longitude: Double? = null
) {
    val hasCoordinates: Boolean
        get() = latitude != null && longitude != null
}
