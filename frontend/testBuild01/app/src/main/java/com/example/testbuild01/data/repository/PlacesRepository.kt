package com.example.testbuild01.data.repository

import com.example.testbuild01.data.model.FindPlaceResponse
import com.example.testbuild01.data.network.PlacesApiService
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory

// Google Places API 조회를 전담하는 레포지토리.
// AI 응답에는 사진이 절대 포함되지 않으므로, placeName만 가지고
// "Find Place from Text"로 place_id/photo_reference를 얻은 뒤
// Places Photo API URL을 구성해 대표 사진 URL을 돌려준다.
class PlacesRepository(
    private val apiKey: String
) {
    companion object {
        private const val PLACES_BASE_URL = "https://maps.googleapis.com/"
        private const val PHOTO_MAX_WIDTH = 480
    }

    private val placesApi: PlacesApiService by lazy {
        Retrofit.Builder()
            .baseUrl(PLACES_BASE_URL)
            .addConverterFactory(GsonConverterFactory.create())
            .build()
            .create(PlacesApiService::class.java)
    }

    // 사진 조회에 실패하거나(장소를 못 찾거나 사진이 없는 경우) 성공하면 null을 콜백에 전달한다.
    fun fetchPlacePhotoUrl(placeName: String, onResult: (String?) -> Unit) {
        if (apiKey.isBlank()) {
            onResult(null)
            return
        }

        placesApi.findPlaceFromText(
            input = placeName,
            inputType = "textquery",
            fields = "place_id,photos",
            apiKey = apiKey
        ).enqueue(object : Callback<FindPlaceResponse> {
            override fun onResponse(
                call: Call<FindPlaceResponse>,
                response: Response<FindPlaceResponse>
            ) {
                val photoReference = response.body()
                    ?.candidates
                    ?.firstOrNull()
                    ?.photos
                    ?.firstOrNull()
                    ?.photoReference

                if (photoReference.isNullOrBlank()) {
                    onResult(null)
                    return
                }

                val photoUrl = "${PLACES_BASE_URL}maps/api/place/photo" +
                    "?maxwidth=$PHOTO_MAX_WIDTH" +
                    "&photo_reference=$photoReference" +
                    "&key=$apiKey"

                onResult(photoUrl)
            }

            override fun onFailure(call: Call<FindPlaceResponse>, t: Throwable) {
                onResult(null)
            }
        })
    }
}
