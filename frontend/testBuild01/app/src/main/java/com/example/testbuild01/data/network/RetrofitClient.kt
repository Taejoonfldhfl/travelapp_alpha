package com.example.testbuild01.data.network

import android.content.Context
import com.example.testbuild01.data.local.TokenManager
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory

object RetrofitClient {
    private const val BASE_URL = "http://10.0.2.2:5020/" // 👈 네 서버 포트번호 적기!

    private lateinit var apiService: TravelApiService

    fun init(context: Context) {
        val tokenManager = TokenManager(context)

        val client = OkHttpClient.Builder()
            .addInterceptor { chain ->
                val originalRequest = chain.request()
                val token = tokenManager.getToken()

                val requestBuilder = originalRequest
                    .newBuilder()

                if (!token.isNullOrEmpty()) {
                    requestBuilder.addHeader(
                        "Authorization",
                        "Bearer $token"
                    )
                }

                val response = chain.proceed(requestBuilder.build())

                // 토큰을 실은 요청이 401이면 그 토큰은 더 이상 유효하지 않다는 뜻이다(만료/서버 재발급 등).
                // 로그인/회원가입 자체의 401(자격 증명 오류)은 세션 만료가 아니므로 제외한다.
                val path = originalRequest.url.encodedPath
                val isAuthEndpoint = path == "/api/User/login" || path == "/api/User/register"
                if (response.code == 401 && !token.isNullOrEmpty() && !isAuthEndpoint) {
                    tokenManager.clearToken()
                    AuthEvents.notifySessionExpired()
                }

                response
            }
            .build()

        val retrofit = Retrofit.Builder()
            .baseUrl(BASE_URL)
            .client(client)
            .addConverterFactory(GsonConverterFactory.create())
            .build()

        apiService = retrofit.create(TravelApiService::class.java)
    }

    val instance: TravelApiService
        get() = apiService
}