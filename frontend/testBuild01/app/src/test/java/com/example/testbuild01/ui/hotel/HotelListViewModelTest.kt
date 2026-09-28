package com.example.testbuild01.ui.hotel

import com.example.testbuild01.data.model.HotelInfoItem
import com.example.testbuild01.data.repository.HotelInfoRepository
import com.example.testbuild01.data.repository.HotelSearchQuery
import com.example.testbuild01.data.repository.HotelSearchResult
import com.example.testbuild01.data.repository.hotelSearchErrorMessage
import com.google.gson.Gson
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class HotelListViewModelTest {

    // 검색 조건을 기록하고, 정해 둔 결과를 돌려준다. pending을 주면 응답을 지연시킨다(로딩 상태 확인용).
    private class FakeRepository(var result: HotelSearchResult = HotelSearchResult.Success(emptyList())) : HotelInfoRepository {
        val queries = mutableListOf<HotelSearchQuery>()
        var pending: CompletableDeferred<HotelSearchResult>? = null

        override suspend fun search(query: HotelSearchQuery): HotelSearchResult {
            queries += query
            return pending?.await() ?: result
        }
    }

    private val withCoords = HotelInfoItem("1", "샘플 광화문 호텔", "서울 종로구", latitude = 37.57, longitude = 126.97)
    private val withoutCoords = HotelInfoItem("2", "샘플 한옥", "서울 종로구 북촌로")

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    @Test
    fun startsIdle() {
        val vm = HotelListViewModel(FakeRepository())

        assertEquals(HotelListContent.Idle, vm.state.value.content)
    }

    @Test
    fun keywordSearch_trimsKeyword_andShowsResults() = runTest {
        val repo = FakeRepository(HotelSearchResult.Success(listOf(withCoords, withoutCoords)))
        val vm = HotelListViewModel(repo)

        vm.onKeywordChange("  광화문 ")
        vm.searchByKeyword()

        assertEquals(listOf(HotelSearchQuery.Keyword("광화문")), repo.queries)
        val content = vm.state.value.content as HotelListContent.Results
        assertEquals(2, content.hotels.size)
    }

    @Test
    fun blankKeyword_doesNotSearch() = runTest {
        val repo = FakeRepository()
        val vm = HotelListViewModel(repo)

        vm.onKeywordChange("   ")
        vm.searchByKeyword()

        assertTrue(repo.queries.isEmpty())
        assertEquals(HotelListContent.Idle, vm.state.value.content)
    }

    @Test
    fun areaSearch_sendsAreaCode_andRemembersSelection() = runTest {
        val repo = FakeRepository(HotelSearchResult.Success(listOf(withCoords)))
        val vm = HotelListViewModel(repo)
        val jeju = TOUR_API_AREAS.first { it.name == "제주" }

        vm.searchByArea(jeju)

        assertEquals(listOf(HotelSearchQuery.Area("39")), repo.queries)
        assertEquals(jeju, vm.state.value.selectedArea)
    }

    @Test
    fun showsLoading_whileWaiting() = runTest {
        val repo = FakeRepository().apply { pending = CompletableDeferred() }
        val vm = HotelListViewModel(repo)

        vm.onKeywordChange("호텔")
        vm.searchByKeyword()
        assertEquals(HotelListContent.Loading, vm.state.value.content)

        repo.pending!!.complete(HotelSearchResult.Success(listOf(withCoords)))
        assertTrue(vm.state.value.content is HotelListContent.Results)
    }

    @Test
    fun emptyResult_showsEmptyState() = runTest {
        val vm = HotelListViewModel(FakeRepository(HotelSearchResult.Success(emptyList())))

        vm.onKeywordChange("없는숙소")
        vm.searchByKeyword()

        assertEquals(HotelListContent.Empty, vm.state.value.content)
    }

    @Test
    fun failure_showsError_andRetryRepeatsLastQuery() = runTest {
        val repo = FakeRepository(HotelSearchResult.Failure("숙소 정보를 가져오지 못했어요."))
        val vm = HotelListViewModel(repo)

        vm.searchByArea(TOUR_API_AREAS.first())
        assertEquals(HotelListContent.Error("숙소 정보를 가져오지 못했어요."), vm.state.value.content)

        repo.result = HotelSearchResult.Success(listOf(withCoords))
        vm.retry()

        assertEquals(listOf(HotelSearchQuery.Area("1"), HotelSearchQuery.Area("1")), repo.queries)
        assertTrue(vm.state.value.content is HotelListContent.Results)
    }

    @Test
    fun onlyHotelsWithCoordinates_areMappable() {
        val content = HotelListContent.Results(listOf(withCoords, withoutCoords, withCoords.copy(contentId = "3", longitude = null)))

        assertEquals(listOf(withCoords), content.mappable)
        assertEquals(3, content.hotels.size) // 좌표가 없어도 목록에는 남는다
    }

    @Test
    fun selectingHotel_opensSheet_andDismissCloses() {
        val vm = HotelListViewModel(FakeRepository())

        vm.onHotelSelected(withoutCoords)
        assertEquals(withoutCoords, vm.state.value.selectedHotel)

        vm.onBookingSheetDismissed()
        assertNull(vm.state.value.selectedHotel)
    }

    @Test
    fun errorMessages_dependOnHttpCode() {
        assertTrue(hotelSearchErrorMessage(401).contains("로그인"))
        assertTrue(hotelSearchErrorMessage(502).contains("가져오지 못했어요"))
        assertTrue(hotelSearchErrorMessage(400).contains("검색 조건"))
        assertTrue(hotelSearchErrorMessage(500).contains("500"))
    }

    @Test
    fun serverJson_withNullCoordinates_parsesAsListItemWithoutCoordinates() {
        val json = """
            [{"contentId":"142785","name":"샘플호텔 서울점","address":"서울 중구","tel":"02-771-0500",
              "imageUrl":"http://tong.visitkorea.or.kr/a.jpg","latitude":37.56,"longitude":126.97},
             {"contentId":"2","name":"좌표 없는 숙소","address":"서울","tel":null,"imageUrl":null,"latitude":null,"longitude":null}]
        """.trimIndent()

        val items = Gson().fromJson(json, Array<HotelInfoItem>::class.java).toList()

        assertTrue(items[0].hasCoordinates)
        assertEquals(37.56, items[0].latitude!!, 0.0)
        assertEquals(false, items[1].hasCoordinates)
        assertNull(items[1].imageUrl)
    }
}
