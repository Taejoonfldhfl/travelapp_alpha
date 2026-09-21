package com.example.testbuild01.ui.ticket

import com.google.mlkit.vision.barcode.common.Barcode
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class BarcodeParserTest {

    @Test
    fun parse_validQr_returnsSuccess() {
        val outcome = BarcodeParser.parse("TICKET-123", Barcode.FORMAT_QR_CODE)

        assertEquals(ScanOutcome.Success("TICKET-123", "QR_CODE"), outcome)
    }

    @Test
    fun parse_nullValue_returnsFailure() {
        val outcome = BarcodeParser.parse(null, Barcode.FORMAT_QR_CODE)

        assertTrue(outcome is ScanOutcome.Failure)
    }

    @Test
    fun parse_blankValue_returnsFailure() {
        val outcome = BarcodeParser.parse("   ", Barcode.FORMAT_PDF417)

        assertTrue(outcome is ScanOutcome.Failure)
    }

    @Test
    fun parse_unknownFormat_stillSucceedsWithUnknownName() {
        // 값은 읽었지만 포맷을 모르는 경우: 저장은 하되 재렌더링은 확인번호로 폴백한다.
        val outcome = BarcodeParser.parse("XYZ", Barcode.FORMAT_UNKNOWN)

        assertEquals(ScanOutcome.Success("XYZ", "UNKNOWN"), outcome)
    }
}
