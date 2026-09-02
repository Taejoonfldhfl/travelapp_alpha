package com.example.testbuild01.data.model

import com.google.gson.annotations.SerializedName

// Google Places API "Find Place from Text" 응답
data class FindPlaceResponse(
    val candidates: List<PlaceCandidate> = emptyList(),
    val status: String = ""
)

data class PlaceCandidate(
    @SerializedName("place_id")
    val placeId: String? = null,
    val photos: List<PlacePhoto>? = null
)

data class PlacePhoto(
    @SerializedName("photo_reference")
    val photoReference: String? = null
)
