package com.example.testbuild01.ui.ticket

import android.graphics.Bitmap
import android.graphics.Color
import com.google.zxing.BarcodeFormat
import com.google.zxing.EncodeHintType
import com.google.zxing.MultiFormatWriter
import com.google.zxing.common.BitMatrix

/** 저장된 barcodeValue/barcodeFormat을 ZXing으로 다시 그린다. 그릴 수 없으면 null을 돌려준다. */
object BarcodeRenderer {

    private const val WIDTH = 1000

    fun render(value: String, formatName: String): Bitmap? {
        val matrix = encode(value, formatName) ?: return null
        val pixels = IntArray(matrix.width * matrix.height) { i ->
            if (matrix.get(i % matrix.width, i / matrix.width)) Color.BLACK else Color.WHITE
        }
        return Bitmap.createBitmap(pixels, matrix.width, matrix.height, Bitmap.Config.ARGB_8888)
    }

    fun encode(value: String, formatName: String): BitMatrix? {
        if (value.isBlank()) return null
        val format = toZxingFormat(formatName) ?: return null
        return try {
            MultiFormatWriter().encode(
                value,
                format,
                WIDTH,
                heightFor(format),
                mapOf(EncodeHintType.CHARACTER_SET to "UTF-8", EncodeHintType.MARGIN to 2)
            )
        } catch (e: Exception) {
            // WriterException, IllegalArgumentException(길이/문자 제한 위반) 등
            null
        }
    }

    fun toZxingFormat(formatName: String): BarcodeFormat? = when (formatName) {
        "QR_CODE" -> BarcodeFormat.QR_CODE
        "PDF_417" -> BarcodeFormat.PDF_417
        "AZTEC" -> BarcodeFormat.AZTEC
        "DATA_MATRIX" -> BarcodeFormat.DATA_MATRIX
        "CODE_128" -> BarcodeFormat.CODE_128
        "CODE_39" -> BarcodeFormat.CODE_39
        "CODE_93" -> BarcodeFormat.CODE_93
        "CODABAR" -> BarcodeFormat.CODABAR
        "EAN_13" -> BarcodeFormat.EAN_13
        "EAN_8" -> BarcodeFormat.EAN_8
        "ITF" -> BarcodeFormat.ITF
        "UPC_A" -> BarcodeFormat.UPC_A
        "UPC_E" -> BarcodeFormat.UPC_E
        else -> null
    }

    private fun heightFor(format: BarcodeFormat): Int = when (format) {
        BarcodeFormat.QR_CODE, BarcodeFormat.AZTEC, BarcodeFormat.DATA_MATRIX -> WIDTH
        BarcodeFormat.PDF_417 -> WIDTH * 2 / 5
        else -> WIDTH * 3 / 10
    }
}
