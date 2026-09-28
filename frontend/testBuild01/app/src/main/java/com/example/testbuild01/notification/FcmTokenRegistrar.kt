package com.example.testbuild01.notification

import android.content.Context
import android.util.Log
import com.example.testbuild01.data.model.DeviceTokenRequest
import com.example.testbuild01.data.network.RetrofitClient
import com.google.firebase.messaging.FirebaseMessaging
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

// 앱 시작/로그인 시 현재 FCM 토큰을 서버(POST /api/User/device-token)에 등록한다.
// google-services.json이 없으면(Firebase 프로젝트 설정 전) FirebaseMessaging 호출 자체가 실패하는데,
// 이 경우 예외를 잡아 조용히 건너뛴다 — 앱의 다른 기능에는 영향이 없어야 한다.
object FcmTokenRegistrar {
    private const val TAG = "FcmTokenRegistrar"

    fun registerCurrentToken(context: Context) {
        try {
            FirebaseMessaging.getInstance().token
                .addOnSuccessListener { token -> sendToServer(token) }
                .addOnFailureListener { e -> Log.w(TAG, "FCM 토큰 발급 실패(Firebase 미설정일 수 있음): ${e.message}") }
        } catch (e: IllegalStateException) {
            // FirebaseApp이 초기화되지 않음(google-services.json 없음).
            Log.w(TAG, "FCM 미설정(google-services.json 없음) — 토큰 등록을 건너뜁니다.")
        }
    }

    fun sendToServer(token: String) {
        CoroutineScope(Dispatchers.IO).launch {
            try {
                RetrofitClient.instance.registerDeviceToken(DeviceTokenRequest(token))
            } catch (e: Exception) {
                // 로그인 전이라 401이 나는 것도 정상 경로다(로그인 후 다시 등록됨).
                Log.w(TAG, "디바이스 토큰 등록 실패: ${e.message}")
            }
        }
    }
}
