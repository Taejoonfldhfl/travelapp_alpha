package com.example.testbuild01.data.network

import com.example.testbuild01.data.model.LoginRequest
import com.example.testbuild01.data.model.LoginResponse
import com.example.testbuild01.data.model.RegisterRequest
import com.example.testbuild01.data.model.RegisterResponse
import com.example.testbuild01.data.model.TripResponse
import com.example.testbuild01.data.model.TripCreateRequest
import com.example.testbuild01.data.model.TripMemberAddRequest
import com.example.testbuild01.data.model.TripMemberResponse
import com.example.testbuild01.data.model.ScheduleResponse
import com.example.testbuild01.data.model.ScheduleCreateRequest
import com.example.testbuild01.data.model.ScheduleUpdateRequest
import com.example.testbuild01.data.model.RouteOptimizationResult
import com.example.testbuild01.data.model.AiChatRequest
import com.example.testbuild01.data.model.AiChatResponse
import com.example.testbuild01.data.model.AiChatSessionResponse
import com.example.testbuild01.data.model.HotelInfoItem
import com.example.testbuild01.data.model.DeviceTokenRequest

import com.example.testbuild01.data.model.BudgetSummary
import com.example.testbuild01.data.model.BudgetUpdateRequest
import com.example.testbuild01.data.model.ExpenseBreakdownItem
import com.example.testbuild01.data.model.ExpenseResponse
import com.example.testbuild01.data.model.ExpenseUpsertRequest
import com.example.testbuild01.data.model.SettlementTransfer
import com.example.testbuild01.data.model.SettlementResult

import retrofit2.Call
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.DELETE
import retrofit2.http.PUT
import retrofit2.http.GET
import retrofit2.http.Query

interface TravelApiService {
    @POST("api/User/login")
    fun login(@Body request: LoginRequest): Call<LoginResponse>

    @POST("api/User/register") // 서버의 회원가입 엔드포인트 주소 확인!
    fun register(@Body request: RegisterRequest): Call<RegisterResponse>

    // FCM 디바이스 토큰 등록/갱신. google-services.json이 없거나 토큰 발급에 실패하면 호출 자체를 하지 않는다.
    @POST("api/User/device-token")
    suspend fun registerDeviceToken(@Body request: DeviceTokenRequest): Response<Unit>

    @GET("api/Trip")
    fun getTrips(): Call<List<TripResponse>>

    @POST("api/Trip")
    fun createTrip(
        @Body request: TripCreateRequest
    ): Call<TripResponse>

    @GET("api/Trip/{id}/members")
    fun getTripMembers(
        @Path("id") tripId: Int
    ): Call<List<TripMemberResponse>>

    @POST("api/Trip/{id}/members")
    fun addTripMember(
        @Path("id") tripId: Int,
        @Body request: TripMemberAddRequest
    ): Call<Void>

    @GET("api/Trip/{tripId}/Schedule")
    fun getSchedules(
        @Path("tripId") tripId: Int
    ): Call<List<ScheduleResponse>>


    @POST("api/Trip/{tripId}/Schedule")
    fun createSchedule(
        @Path("tripId") tripId: Int,
        @Body request: ScheduleCreateRequest
    ): Call<ScheduleResponse>


    @PUT("api/Trip/{tripId}/Schedule/{scheduleId}")
    fun updateSchedule(
        @Path("tripId") tripId: Int,
        @Path("scheduleId") scheduleId: Int,
        @Body request: ScheduleUpdateRequest
    ): Call<ScheduleResponse>


    @DELETE("api/Trip/{tripId}/Schedule/{scheduleId}")
    fun deleteSchedule(
        @Path("tripId") tripId: Int,
        @Path("scheduleId") scheduleId: Int
    ): Call<Void>

    // date: "yyyy-MM-dd". apply=false면 미리보기만, apply=true면 계산한 순서를 실제로 저장.
    @GET("api/Trip/{tripId}/RouteOptimization/{date}")
    fun optimizeRoute(
        @Path("tripId") tripId: Int,
        @Path("date") date: String,
        @Query("apply") apply: Boolean
    ): Call<RouteOptimizationResult>

    @POST("api/Trip/{tripId}/AiChat/sessions")
    fun createAiChatSession(
        @Path("tripId") tripId: Int
    ): Call<AiChatSessionResponse>

    @POST("api/Trip/{tripId}/AiChat/sessions/{sessionId}/messages")
    fun sendAiChatMessage(
        @Path("tripId") tripId: Int,
        @Path("sessionId") sessionId: Int,
        @Body request: AiChatRequest
    ): Call<AiChatResponse>

    // ---- 호텔 티켓 ↔ 일정 연동 (suspend) ----

    @GET("api/Trip")
    suspend fun getTripsSuspend(): Response<List<TripResponse>>

    @GET("api/Trip/{tripId}/Schedule/{scheduleId}")
    suspend fun getScheduleSuspend(
        @Path("tripId") tripId: Int,
        @Path("scheduleId") scheduleId: Int
    ): Response<ScheduleResponse>

    @POST("api/Trip/{tripId}/Schedule")
    suspend fun createScheduleSuspend(
        @Path("tripId") tripId: Int,
        @Body request: ScheduleCreateRequest
    ): Response<ScheduleResponse>

    @PUT("api/Trip/{tripId}/Schedule/{scheduleId}")
    suspend fun updateScheduleSuspend(
        @Path("tripId") tripId: Int,
        @Path("scheduleId") scheduleId: Int,
        @Body request: ScheduleUpdateRequest
    ): Response<ScheduleResponse>

    @DELETE("api/Trip/{tripId}/Schedule/{scheduleId}")
    suspend fun deleteScheduleSuspend(
        @Path("tripId") tripId: Int,
        @Path("scheduleId") scheduleId: Int
    ): Response<Void>

    // ---- 가계부 (suspend) ----

    @GET("api/Trip/{id}/members")
    suspend fun getTripMembersSuspend(@Path("id") tripId: Int): Response<List<TripMemberResponse>>

    @GET("api/Trip/{tripId}/Schedule")
    suspend fun getSchedulesSuspend(@Path("tripId") tripId: Int): Response<List<ScheduleResponse>>

    @GET("api/Trip/{tripId}/expenses")
    suspend fun getExpenses(@Path("tripId") tripId: Int): Response<List<ExpenseResponse>>

    @POST("api/Trip/{tripId}/expenses")
    suspend fun createExpense(
        @Path("tripId") tripId: Int,
        @Body request: ExpenseUpsertRequest
    ): Response<ExpenseResponse>

    @PUT("api/Expense/{id}")
    suspend fun updateExpense(
        @Path("id") expenseId: Int,
        @Body request: ExpenseUpsertRequest
    ): Response<ExpenseResponse>

    @DELETE("api/Expense/{id}")
    suspend fun deleteExpense(@Path("id") expenseId: Int): Response<Void>

    @PUT("api/Trip/{tripId}/budget")
    suspend fun setBudget(
        @Path("tripId") tripId: Int,
        @Body request: BudgetUpdateRequest
    ): Response<Void>

    @GET("api/Trip/{tripId}/budget-summary")
    suspend fun getBudgetSummary(@Path("tripId") tripId: Int): Response<BudgetSummary>

    @GET("api/Trip/{tripId}/expense-breakdown")
    suspend fun getExpenseBreakdown(@Path("tripId") tripId: Int): Response<List<ExpenseBreakdownItem>>

    @GET("api/Trip/{tripId}/settlement")
    suspend fun getSettlement(@Path("tripId") tripId: Int): Response<List<SettlementTransfer>>

    // 정산 확정: 현재 정산 결과를 스냅샷으로 저장하고, 같은 여행 멤버들에게 알림을 보낸다.
    @POST("api/Trip/{tripId}/Expense/settlements/finalize")
    suspend fun finalizeSettlement(@Path("tripId") tripId: Int): Response<SettlementResult>

    @GET("api/Trip/{tripId}/Expense/settlements/{settlementId}")
    suspend fun getFinalizedSettlement(
        @Path("tripId") tripId: Int,
        @Path("settlementId") settlementId: Int
    ): Response<SettlementResult>

    // 숙박시설 정보 검색(TourAPI). keyword / areaCode / lat+lng 중 하나만 채운다(null 파라미터는 전송되지 않음).
    @GET("api/HotelInfo/search")
    suspend fun searchHotelInfo(
        @Query("keyword") keyword: String? = null,
        @Query("areaCode") areaCode: String? = null,
        @Query("lat") latitude: Double? = null,
        @Query("lng") longitude: Double? = null,
        @Query("radius") radiusMeters: Int? = null,
        @Query("page") page: Int = 1
    ): Response<List<HotelInfoItem>>
}