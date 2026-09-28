package com.example.testbuild01.ui.aichat

import com.example.testbuild01.data.model.AiPlaceRecommendation
import com.google.gson.Gson
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class RecommendationCardMapperTest {

    private fun recommendation(imageUrl: String?) = AiPlaceRecommendation(
        placeName = "경복궁",
        description = "조선의 정궁",
        suggestedStartTime = "2026-10-01T01:00:00Z",
        suggestedEndTime = "2026-10-01T03:00:00Z",
        placeId = "tmap:경복궁",
        latitude = 37.5796,
        longitude = 126.9770,
        imageUrl = imageUrl
    )

    @Test
    fun serverImageUrl_isUsedDirectly_withoutExtraLookup() {
        val card = recommendation("https://img.example/gyeongbokgung.jpg").toRecommendationCard("card_0")

        assertEquals("https://img.example/gyeongbokgung.jpg", card.photoUrl)
        assertFalse(card.photoLoading)
        assertFalse(card.needsPhotoLookup())
    }

    @Test
    fun nullImageUrl_fallsBackToPhotoLookup() {
        val card = recommendation(null).toRecommendationCard("card_0")

        assertNull(card.photoUrl)
        assertTrue(card.photoLoading)
        assertTrue(card.needsPhotoLookup())
    }

    @Test
    fun blankImageUrl_isTreatedAsNoImage() {
        val card = recommendation("   ").toRecommendationCard("card_0")

        assertNull(card.photoUrl)
        assertTrue(card.needsPhotoLookup())
    }

    @Test
    fun lookupFinishedWithoutPhoto_showsPlaceholderAndDoesNotLookUpAgain() {
        val card = recommendation(null).toRecommendationCard("card_0").copy(photoUrl = null, photoLoading = false)

        assertFalse(card.needsPhotoLookup())
    }

    @Test
    fun mapsTextAndCoordinates() {
        val card = recommendation(null).toRecommendationCard("card_7")

        assertEquals("card_7", card.id)
        assertEquals("경복궁", card.placeName)
        assertEquals("조선의 정궁", card.description)
        assertEquals(37.5796, card.latitude!!, 0.0)
        assertEquals(126.9770, card.longitude!!, 0.0)
    }

    @Test
    fun serverJson_imageUrlField_isParsed_andMissingFieldIsNull() {
        val gson = Gson()
        val withImage = gson.fromJson(
            """{"placeName":"경복궁","description":"d","suggestedStartTime":"s","suggestedEndTime":"e","imageUrl":"https://img.example/a.jpg"}""",
            AiPlaceRecommendation::class.java
        )
        val withoutImage = gson.fromJson(
            """{"placeName":"경복궁","description":"d","suggestedStartTime":"s","suggestedEndTime":"e","imageUrl":null}""",
            AiPlaceRecommendation::class.java
        )

        assertEquals("https://img.example/a.jpg", withImage.imageUrl)
        assertNull(withoutImage.imageUrl)
    }
}
