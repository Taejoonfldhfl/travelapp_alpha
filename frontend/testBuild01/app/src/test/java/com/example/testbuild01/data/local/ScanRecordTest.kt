package com.example.testbuild01.data.local

import org.junit.Assert.assertEquals
import org.junit.Test

class ScanRecordTest {

    @Test
    fun toHex_keepsBinaryBytesExactly() {
        val bytes = byteArrayOf(0x00, 0x1f, 0x7f, 0xff.toByte(), 0x41)

        assertEquals("001f7fff41", bytes.toHex())
    }

    @Test
    fun toHex_emptyArray_returnsEmptyString() {
        assertEquals("", byteArrayOf().toHex())
    }
}
