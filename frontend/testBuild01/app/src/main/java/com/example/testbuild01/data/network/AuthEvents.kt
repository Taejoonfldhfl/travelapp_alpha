package com.example.testbuild01.data.network

import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow

/**
 * 인증이 필요한 요청이 401을 받아 RetrofitClient의 인터셉터가 토큰을 지운 뒤 알리는 이벤트.
 * UI(TravelApp)가 이를 구독해 토스트를 띄우고 로그인 화면으로 돌려보낸다.
 */
object AuthEvents {
    private val _sessionExpired = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val sessionExpired: SharedFlow<Unit> = _sessionExpired.asSharedFlow()

    fun notifySessionExpired() {
        _sessionExpired.tryEmit(Unit)
    }
}
