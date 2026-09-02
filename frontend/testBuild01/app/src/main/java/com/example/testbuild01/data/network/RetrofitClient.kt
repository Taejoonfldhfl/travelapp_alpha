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

                chain.proceed(requestBuilder.build())
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