package com.example.testbuild01.data.network

import android.content.Context
import com.example.testbuild01.data.local.TokenManager
import kotlinx.coroutines.withTimeoutOrNull

/**
 * 로그아웃 절차: 서버에 등록된 FCM 디바이스 토큰을 먼저 지운다(그 호출 자체에 JWT가 필요하므로
 * 로컬 토큰을 지우기 전에 순서를 지킨다). 서버 호출이 실패하거나 오래 걸려도 로컬 토큰은 반드시 지운다.
 */
object AuthSession {
    private const val DEVICE_TOKEN_DELETE_TIMEOUT_MS = 3_000L

    suspend fun logout(context: Context) {
        val tokenManager = TokenManager(context)
        try {
            withTimeoutOrNull(DEVICE_TOKEN_DELETE_TIMEOUT_MS) {
                RetrofitClient.instance.deleteDeviceToken()
            }
        } catch (e: Exception) {
            // 서버 호출 실패는 무시한다 - 로컬 로그아웃은 항상 진행돼야 한다.
        } finally {
            tokenManager.clearToken()
        }
    }
}
