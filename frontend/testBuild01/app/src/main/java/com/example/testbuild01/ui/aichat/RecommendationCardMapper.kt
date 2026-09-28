package com.example.testbuild01.ui.aichat

import com.example.testbuild01.data.model.AiPlaceRecommendation

// 서버 추천 하나를 채팅 카드 상태로 바꾼다. 서버가 대표 사진(imageUrl)을 줬으면 그걸 바로 쓰고(추가 조회 없음),
// 없으면 photoLoading=true로 두어 ViewModel이 Google Places로 사진을 따로 조회하게 한다.
internal fun AiPlaceRecommendation.toRecommendationCard(id: String): ChatMessage.RecommendationCard {
    val serverImageUrl = imageUrl?.takeIf { it.isNotBlank() }

    return ChatMessage.RecommendationCard(
        id = id,
        placeName = placeName,
        description = description,
        suggestedStartTime = suggestedStartTime,
        suggestedEndTime = suggestedEndTime,
        photoUrl = serverImageUrl,
        photoLoading = serverImageUrl == null,
        // 규칙 2: 서버가 이미 좌표를 확정한 추천만 내려주므로 그대로 신뢰해 사용한다.
        latitude = latitude,
        longitude = longitude
    )
}

// 카드에 별도 사진 조회가 필요한지(서버가 사진을 주지 않았고 아직 조회 전인지).
internal fun ChatMessage.RecommendationCard.needsPhotoLookup(): Boolean =
    photoUrl == null && photoLoading
