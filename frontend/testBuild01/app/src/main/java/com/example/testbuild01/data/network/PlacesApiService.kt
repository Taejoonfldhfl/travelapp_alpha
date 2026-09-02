package com.example.testbuild01.data.network

import com.example.testbuild01.data.model.FindPlaceResponse
import retrofit2.Call
import retrofit2.http.GET
import retrofit2.http.Query

interface PlacesApiService {
    @GET("maps/api/place/findplacefromtext/json")
    fun findPlaceFromText(
        @Query("input") input: String,
        @Query("inputtype") inputType: String,
        @Query("fields") fields: String,
        @Query("key") apiKey: String
    ): Call<FindPlaceResponse>
}
