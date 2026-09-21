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

import retrofit2.Call
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

    @POST("api/Trip/{id}/member")
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
}