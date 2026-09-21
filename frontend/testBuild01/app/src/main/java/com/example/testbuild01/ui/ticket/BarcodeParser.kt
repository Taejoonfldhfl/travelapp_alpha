package com.example.testbuild01.ui.ticket

import com.google.mlkit.vision.barcode.common.Barcode

sealed interface ScanOutcome {
    data class Success(val value: String, val format: String) : ScanOutcome

    data class Failure(val reason: Reason) : ScanOutcome

    enum class Reason {
        /** 바코드는 감지됐지만 문자열 값을 얻지 못함 (바이너리 전용, 손상 등) */
        EMPTY_VALUE
    }
}

/** ML Kit 스캔 결과를 앱에서 쓰는 값으로 변환한다. 실패 케이스는 예외 대신 [ScanOutcome.Failure]로 돌려준다. */
object BarcodeParser {

    fun parse(rawValue: String?, formatCode: Int): ScanOutcome {
        val value = rawValue?.trim()
        if (value.isNullOrEmpty()) return ScanOutcome.Failure(ScanOutcome.Reason.EMPTY_VALUE)
        return ScanOutcome.Success(value, formatName(formatCode))
    }

    fun formatName(formatCode: Int): String = when (formatCode) {
        Barcode.FORMAT_QR_CODE -> "QR_CODE"
        Barcode.FORMAT_PDF417 -> "PDF_417"
        Barcode.FORMAT_AZTEC -> "AZTEC"
        Barcode.FORMAT_DATA_MATRIX -> "DATA_MATRIX"
        Barcode.FORMAT_CODE_128 -> "CODE_128"
        Barcode.FORMAT_CODE_39 -> "CODE_39"
        Barcode.FORMAT_CODE_93 -> "CODE_93"
        Barcode.FORMAT_CODABAR -> "CODABAR"
        Barcode.FORMAT_EAN_13 -> "EAN_13"
        Barcode.FORMAT_EAN_8 -> "EAN_8"
        Barcode.FORMAT_ITF -> "ITF"
        Barcode.FORMAT_UPC_A -> "UPC_A"
        Barcode.FORMAT_UPC_E -> "UPC_E"
        else -> "UNKNOWN"
    }
}
