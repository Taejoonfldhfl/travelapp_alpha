package com.example.testbuild01.data.local

import android.content.Context
import android.util.Log
import com.example.testbuild01.BuildConfig
import com.google.gson.Gson
import java.io.File

/** 스캔 한 건의 원본 결과. 인식이 이상할 때 원인을 확인하기 위한 진단용 기록이다. */
data class ScanRecord(
    val timestamp: Long,
    /** SUCCESS: 값을 읽음, EMPTY: 바코드는 감지됐지만 값 없음, ERROR: 스캔 중 예외 */
    val outcome: String,
    val format: String,
    val valueType: Int?,
    val rawValue: String?,
    val displayValue: String?,
    /** rawValue는 문자열로 변환되며 손실될 수 있어, 바이너리 바코드 확인용으로 원본 바이트를 16진수로 함께 남긴다. */
    val rawBytesHex: String?,
    val message: String? = null
)

fun ByteArray.toHex(): String = joinToString("") { "%02x".format(it) }

/**
 * 스캔 결과를 JSON Lines 파일과 logcat(TAG)에 남긴다. 티켓 원본 값이 평문으로 저장되므로 디버그 빌드에서만 동작한다.
 *
 * 확인 방법:
 *   adb logcat -s TicketDebug
 *   adb pull /sdcard/Android/data/<패키지>/files/scan_results.jsonl
 */
class ScanResultStore(context: Context) {
    private val file: File = File(context.getExternalFilesDir(null) ?: context.filesDir, FILE_NAME)
    private val gson = Gson()

    @Synchronized
    fun append(record: ScanRecord) {
        if (!BuildConfig.DEBUG) return
        val line = gson.toJson(record)
        Log.d(TAG, "scan $line")
        try {
            file.appendText(line + "\n")
        } catch (e: Exception) {
            Log.w(TAG, "스캔 기록 저장 실패", e)
        }
    }

    companion object {
        const val TAG = "TicketDebug"
        const val FILE_NAME = "scan_results.jsonl"
    }
}
