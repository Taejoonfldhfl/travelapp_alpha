package com.example.testbuild01.data.repository

import com.example.testbuild01.data.model.FindPlaceResponse
import com.example.testbuild01.data.network.PlacesApiService
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory

// Google Places API 조회를 전담하는 레포지토리.
// AI 응답에는 사진/좌표가 절대 포함되지 않으므로, placeName만 가지고
// "Find Place from Text"로 place_id/photo_reference/geometry를 얻은 뒤
// 대표 사진 URL과 좌표(위도/경도)를 함께 돌려준다.
// 좌표는 이후 일정에 추가할 때 ScheduleCreateRequest에 실어 보내 경로 최적화(RouteOptimization)
// 대상에 포함되도록 하는 데 쓰인다.
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

    // 장소를 찾지 못하면 사진/좌표 모두 null인 결과를 콜백에 전달한다.
    fun fetchPlaceDetails(placeName: String, onResult: (PlaceDetails) -> Unit) {
        if (apiKey.isBlank()) {
            onResult(PlaceDetails())
            return
        }

        placesApi.findPlaceFromText(
            input = placeName,
            inputType = "textquery",
            fields = "place_id,photos,geometry",
            apiKey = apiKey
        ).enqueue(object : Callback<FindPlaceResponse> {
            override fun onResponse(
                call: Call<FindPlaceResponse>,
                response: Response<FindPlaceResponse>
            ) {
                val candidate = response.body()?.candidates?.firstOrNull()

                val photoReference = candidate?.photos?.firstOrNull()?.photoReference
                val photoUrl = if (photoReference.isNullOrBlank()) {
                    null
                } else {
                    "${PLACES_BASE_URL}maps/api/place/photo" +
                        "?maxwidth=$PHOTO_MAX_WIDTH" +
                        "&photo_reference=$photoReference" +
                        "&key=$apiKey"
                }

                onResult(
                    PlaceDetails(
                        photoUrl = photoUrl,
                        latitude = candidate?.geometry?.location?.lat,
                        longitude = candidate?.geometry?.location?.lng
                    )
                )
            }

            override fun onFailure(call: Call<FindPlaceResponse>, t: Throwable) {
                onResult(PlaceDetails())
            }
        })
    }
}

data class PlaceDetails(
    val photoUrl: String? = null,
    val latitude: Double? = null,
    val longitude: Double? = null
)
