package com.example.testbuild01.ui.expense

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
import com.example.testbuild01.ui.common.ocr.OcrScannerScaffold

// 영수증에서 금액만 인식한다. 인식 결과는 바로 저장하지 않고 항상 확인/수정 화면으로 넘긴다.
@Composable
fun ReceiptScannerScreen(
    viewModel: ExpenseViewModel,
    onBack: () -> Unit,
    onConfirm: () -> Unit
) {
    var detectedAmount by remember { mutableStateOf<Long?>(null) }

    OcrScannerScaffold(
        title = "영수증 스캔",
        onBack = onBack,
        onTextRecognized = { text -> extractPrice(text)?.let { price -> detectedAmount = price } },
        noPermissionContent = { retryPermission ->
            Text("영수증 인식을 위해 카메라 권한이 필요합니다.")
            Spacer(Modifier.height(8.dp))
            Button(onClick = retryPermission) { Text("권한 다시 요청하기") }
            TextButton(onClick = {
                viewModel.updateScannedAmount(null)
                onConfirm()
            }) { Text("직접 입력하기") }
        },
        bottomContent = {
            Text(
                detectedAmount?.let { "인식된 금액: ${"%,d".format(it)}원" } ?: "영수증을 비춰주세요",
                style = MaterialTheme.typography.titleLarge
            )
            Button(
                onClick = {
                    viewModel.updateScannedAmount(detectedAmount)
                    onConfirm()
                },
                enabled = detectedAmount != null,
                modifier = Modifier.fillMaxWidth()
            ) { Text("확인/수정하기") }
        }
    )
}

// 가장 큰 숫자를 총액으로 본다. 어디까지나 추정값이라 사용자가 확인 화면에서 수정한다.
internal fun extractPrice(text: String): Long? {
    val regex = Regex("""\d{1,3}(,\d{3})+|\d{4,}""")
    return regex.findAll(text)
        .mapNotNull { it.value.replace(",", "").toLongOrNull() }
        .filter { it > 100 }
        .maxOrNull()
}
