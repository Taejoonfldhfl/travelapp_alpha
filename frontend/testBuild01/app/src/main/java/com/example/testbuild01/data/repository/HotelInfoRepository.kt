package com.example.testbuild01.data.repository

import com.example.testbuild01.data.model.HotelInfoItem
import com.example.testbuild01.data.network.TravelApiService
import java.io.IOException

// 숙박시설 검색 조건. 서버 API가 한 번에 한 가지 방식만 받으므로 sealed로 구분한다.
sealed class HotelSearchQuery {
    data class Keyword(val keyword: String) : HotelSearchQuery()
    data class Area(val areaCode: String) : HotelSearchQuery()
}

sealed class HotelSearchResult {
    data class Success(val hotels: List<HotelInfoItem>) : HotelSearchResult()
    data class Failure(val message: String) : HotelSearchResult()
}

interface HotelInfoRepository {
    suspend fun search(query: HotelSearchQuery): HotelSearchResult
}

class RemoteHotelInfoRepository(
    private val api: () -> TravelApiService
) : HotelInfoRepository {

    override suspend fun search(query: HotelSearchQuery): HotelSearchResult = try {
        val response = when (query) {
            is HotelSearchQuery.Keyword -> api().searchHotelInfo(keyword = query.keyword)
            is HotelSearchQuery.Area -> api().searchHotelInfo(areaCode = query.areaCode)
        }
        val body = response.body()
        if (response.isSuccessful && body != null) {
            HotelSearchResult.Success(body)
        } else {
            HotelSearchResult.Failure(hotelSearchErrorMessage(response.code()))
        }
    } catch (e: IOException) {
        HotelSearchResult.Failure("서버에 연결하지 못했어요. 네트워크를 확인하고 다시 시도해 주세요.")
    }
}

// 서버 응답 코드별 사용자 문구.
internal fun hotelSearchErrorMessage(httpCode: Int): String = when (httpCode) {
    401, 403 -> "로그인이 필요해요. 다시 로그인한 뒤 시도해 주세요."
    400 -> "검색 조건을 확인해 주세요."
    502, 503, 504 -> "숙소 정보를 가져오지 못했어요. 잠시 후 다시 시도해 주세요."
    else -> "숙소 검색에 실패했어요. ($httpCode)"
}
