package com.example.testbuild01.ui.hotel

import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performTextInput
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertFalse
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class HotelDetailFormTest {

    @get:Rule
    val composeTestRule = createComposeRule()

    @Test
    fun saveButton_staysDisabled_untilAllRequiredFieldsAreFilled() {
        var saved = false
        composeTestRule.setContent {
            HotelDetailForm(
                title = "테스트",
                seed = HotelFormSeed(),
                onBack = {},
                onSave = { saved = true }
            )
        }

        // 아무 것도 입력하지 않은 초기 상태
        composeTestRule.onNodeWithText("저장").assertIsNotEnabled()

        // 텍스트 필드는 채웠지만 체크인/체크아웃 일시(필수)는 아직 선택하지 않은 상태
        composeTestRule.onNodeWithText("호텔명").performTextInput("서울 스퀘어 호텔")
        composeTestRule.onNodeWithText("주소").performTextInput("서울 중구 세종대로 123")
        composeTestRule.onNodeWithText("투숙 인원").performTextInput("2")
        composeTestRule.onNodeWithText("확인번호").performTextInput("ABC-123456")
        composeTestRule.onNodeWithText("예약자명").performTextInput("홍길동")

        composeTestRule.onNodeWithText("저장").assertIsNotEnabled()
        assertFalse(saved)
    }
}
