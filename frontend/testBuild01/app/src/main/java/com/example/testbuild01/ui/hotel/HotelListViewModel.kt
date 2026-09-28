package com.example.testbuild01.ui.hotel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.testbuild01.data.model.HotelInfoItem
import com.example.testbuild01.data.network.RetrofitClient
import com.example.testbuild01.data.repository.HotelInfoRepository
import com.example.testbuild01.data.repository.HotelSearchQuery
import com.example.testbuild01.data.repository.HotelSearchResult
import com.example.testbuild01.data.repository.RemoteHotelInfoRepository
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/** TourAPI 지역코드(시/도). 서버가 그대로 areaBasedList2의 areaCode로 넘긴다. */
data class TourApiArea(val code: String, val name: String)

val TOUR_API_AREAS: List<TourApiArea> = listOf(
    TourApiArea("1", "서울"), TourApiArea("2", "인천"), TourApiArea("3", "대전"), TourApiArea("4", "대구"),
    TourApiArea("5", "광주"), TourApiArea("6", "부산"), TourApiArea("7", "울산"), TourApiArea("8", "세종"),
    TourApiArea("31", "경기"), TourApiArea("32", "강원"), TourApiArea("33", "충북"), TourApiArea("34", "충남"),
    TourApiArea("35", "경북"), TourApiArea("36", "경남"), TourApiArea("37", "전북"), TourApiArea("38", "전남"),
    TourApiArea("39", "제주")
)

enum class HotelSearchMode { KEYWORD, AREA }

sealed class HotelListContent {
    /** 아직 검색하지 않음. */
    data object Idle : HotelListContent()
    data object Loading : HotelListContent()
    data class Error(val message: String) : HotelListContent()
    /** 검색했지만 결과 0건. */
    data object Empty : HotelListContent()
    data class Results(val hotels: List<HotelInfoItem>) : HotelListContent() {
        /** 지도 마커로 찍을 수 있는(좌표가 있는) 숙소만. 좌표 없는 숙소는 목록에만 표시한다. */
        val mappable: List<HotelInfoItem> get() = hotels.filter { it.hasCoordinates }
    }
}

data class HotelListUiState(
    val mode: HotelSearchMode = HotelSearchMode.KEYWORD,
    val keyword: String = "",
    val selectedArea: TourApiArea? = null,
    val content: HotelListContent = HotelListContent.Idle,
    /** 탭해서 바텀시트(예약 사이트 선택)를 띄운 숙소. */
    val selectedHotel: HotelInfoItem? = null
) {
    val canSearchKeyword: Boolean get() = keyword.isNotBlank()
}

class HotelListViewModel(
    private val repository: HotelInfoRepository = RemoteHotelInfoRepository { RetrofitClient.instance }
) : ViewModel() {

    private val _state = MutableStateFlow(HotelListUiState())
    val state: StateFlow<HotelListUiState> = _state.asStateFlow()

    private var lastQuery: HotelSearchQuery? = null
    private var searchJob: Job? = null

    fun onModeChange(mode: HotelSearchMode) = _state.update { it.copy(mode = mode) }

    fun onKeywordChange(keyword: String) = _state.update { it.copy(keyword = keyword) }

    fun searchByKeyword() {
        val keyword = _state.value.keyword.trim()
        if (keyword.isEmpty()) return
        search(HotelSearchQuery.Keyword(keyword))
    }

    fun searchByArea(area: TourApiArea) {
        _state.update { it.copy(selectedArea = area) }
        search(HotelSearchQuery.Area(area.code))
    }

    /** 오류 화면의 "다시 시도". */
    fun retry() {
        lastQuery?.let(::search)
    }

    fun onHotelSelected(hotel: HotelInfoItem) = _state.update { it.copy(selectedHotel = hotel) }

    fun onBookingSheetDismissed() = _state.update { it.copy(selectedHotel = null) }

    private fun search(query: HotelSearchQuery) {
        lastQuery = query
        // 이전 검색이 늦게 끝나 새 결과를 덮어쓰지 않게 취소한다.
        searchJob?.cancel()
        _state.update { it.copy(content = HotelListContent.Loading, selectedHotel = null) }

        searchJob = viewModelScope.launch {
            val content = when (val result = repository.search(query)) {
                is HotelSearchResult.Success ->
                    if (result.hotels.isEmpty()) HotelListContent.Empty else HotelListContent.Results(result.hotels)
                is HotelSearchResult.Failure -> HotelListContent.Error(result.message)
            }
            _state.update { it.copy(content = content) }
        }
    }
}

class HotelListViewModelFactory : ViewModelProvider.Factory {
    override fun <T : ViewModel> create(modelClass: Class<T>): T {
        @Suppress("UNCHECKED_CAST")
        return HotelListViewModel() as T
    }
}
