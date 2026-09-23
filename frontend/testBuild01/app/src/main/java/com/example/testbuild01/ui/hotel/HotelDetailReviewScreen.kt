package com.example.testbuild01.ui.hotel

import androidx.compose.runtime.Composable
import com.example.testbuild01.data.hotel.HotelOcrCandidate
import com.example.testbuild01.data.local.HotelDetail

/** OCR 인식 결과를 그대로 저장하지 않고 항상 이 화면을 거쳐 확인/수정하게 한다. */
@Composable
fun HotelDetailReviewScreen(
    candidate: HotelOcrCandidate,
    onBack: () -> Unit,
    onSave: (HotelDetail) -> Unit
) {
    HotelDetailForm(
        title = "확인서 내용 확인",
        seed = candidate.toFormSeed(),
        onBack = onBack,
        onSave = onSave
    )
}
