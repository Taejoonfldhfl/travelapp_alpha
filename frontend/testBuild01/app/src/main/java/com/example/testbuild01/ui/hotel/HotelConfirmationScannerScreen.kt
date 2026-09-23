package com.example.testbuild01.ui.hotel

import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.hotel.HotelOcrCandidate
import com.example.testbuild01.data.hotel.extractHotelFields
import com.example.testbuild01.ui.common.ocr.OcrScannerScaffold

/**
 * 기존 ReceiptScannerScreen과 같은 ML Kit OCR 기반이지만, 호텔명/체크인·아웃/확인번호를 뽑는다.
 * 인식 결과는 절대 바로 저장하지 않고 [onRecognized]를 통해 확인/수정 화면으로 넘어간다.
 */
@Composable
fun HotelConfirmationScannerScreen(
    onRecognized: (HotelOcrCandidate) -> Unit,
    onManualEntry: () -> Unit,
    onBack: () -> Unit
) {
    var candidate by remember { mutableStateOf(HotelOcrCandidate()) }

    OcrScannerScaffold(
        title = "호텔 확인서 스캔",
        onBack = onBack,
        onTextRecognized = { text -> candidate = extractHotelFields(text) },
        noPermissionContent = { retryPermission ->
            Text("확인서 인식을 위해 카메라 권한이 필요합니다.")
            Spacer(Modifier.height(8.dp))
            Button(onClick = retryPermission) { Text("권한 다시 요청하기") }
            TextButton(onClick = onManualEntry) { Text("직접 입력하기") }
        },
        bottomContent = {
            val summary = if (candidate.hotelName.isBlank() && candidate.confirmationNumber.isBlank()) {
                "호텔 확인서를 비춰주세요"
            } else {
                "인식됨: ${candidate.hotelName.ifBlank { "(호텔명 미인식)" }} · " +
                    "확인번호 ${candidate.confirmationNumber.ifBlank { "미인식" }}"
            }
            Text(summary, style = MaterialTheme.typography.titleMedium)
            Button(onClick = { onRecognized(candidate) }, modifier = Modifier.fillMaxWidth()) {
                Text("확인/수정하기")
            }
            TextButton(onClick = onManualEntry) { Text("직접 입력으로 전환") }
        }
    )
}
