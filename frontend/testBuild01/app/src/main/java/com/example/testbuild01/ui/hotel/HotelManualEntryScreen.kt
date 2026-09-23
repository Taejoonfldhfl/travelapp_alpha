package com.example.testbuild01.ui.hotel

import androidx.compose.runtime.Composable
import com.example.testbuild01.data.local.HotelDetail

/** OCR 실패 시 폴백, 그리고 기존 호텔 예약 수정에도 같은 폼을 쓴다. */
@Composable
fun HotelManualEntryScreen(
    editing: HotelDetail?,
    onBack: () -> Unit,
    onSave: (HotelDetail) -> Unit
) {
    HotelDetailForm(
        title = if (editing != null) "호텔 예약 수정" else "호텔 예약 직접 입력",
        seed = editing?.toFormSeed() ?: HotelFormSeed(),
        onBack = onBack,
        onSave = onSave
    )
}
