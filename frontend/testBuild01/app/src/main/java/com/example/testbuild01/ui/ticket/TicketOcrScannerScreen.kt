package com.example.testbuild01.ui.ticket

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
import com.example.testbuild01.data.ticket.TicketOcrCandidate
import com.example.testbuild01.data.ticket.extractTicketFields
import com.example.testbuild01.ui.common.ocr.OcrScannerScaffold

/**
 * 바코드가 없거나 인식이 안 되는 항공권/버스표를 위한 문자(OCR) 기반 대안 스캔 경로.
 * HotelConfirmationScannerScreen과 같은 ML Kit OCR 뼈대를 쓰되, 편명/구간/출발일시/확인번호를 뽑는다.
 * 인식 결과는 절대 바로 저장하지 않고 [onRecognized]를 통해 확인/수정 화면(TicketManualEntryScreen)으로 넘어간다.
 */
@Composable
fun TicketOcrScannerScreen(
    onRecognized: (TicketOcrCandidate) -> Unit,
    onManualEntry: () -> Unit,
    onBack: () -> Unit
) {
    var candidate by remember { mutableStateOf(TicketOcrCandidate()) }

    OcrScannerScaffold(
        title = "티켓 문자 인식(OCR)",
        onBack = onBack,
        onTextRecognized = { text -> candidate = extractTicketFields(text) },
        noPermissionContent = { retryPermission ->
            Text("티켓 인식을 위해 카메라 권한이 필요합니다.")
            Spacer(Modifier.height(8.dp))
            Button(onClick = retryPermission) { Text("권한 다시 요청하기") }
            TextButton(onClick = onManualEntry) { Text("직접 입력하기") }
        },
        bottomContent = {
            val summary = if (candidate.title.isBlank() && candidate.confirmationNumber.isBlank()) {
                "티켓을 비춰주세요"
            } else {
                "인식됨: ${candidate.title.ifBlank { "(정보 미인식)" }} · " +
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
