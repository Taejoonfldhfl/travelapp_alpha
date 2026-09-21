package com.example.testbuild01.data.local

import android.content.Context

class TokenManager(context: Context) {
    private val prefs = context.getSharedPreferences(
        "auth_prefs",
        Context.MODE_PRIVATE
    )

    fun saveToken(token: String) {
        prefs.edit()
            .putString("jwt_token", token)
            .apply()
    }

    fun getToken(): String? {
        return prefs.getString("jwt_token", null)
    }

    // JWT payload 의 NameIdentifier 클레임에서 현재 로그인한 사용자 ID 를 꺼낸다.
    fun getUserId(): Int? {
        val payload = getToken()?.split(".")?.getOrNull(1) ?: return null
        return try {
            val json = org.json.JSONObject(
                String(android.util.Base64.decode(payload, android.util.Base64.URL_SAFE))
            )
            listOf(
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                "nameid",
                "sub"
            ).firstNotNullOfOrNull { key -> json.opt(key)?.toString()?.toIntOrNull() }
        } catch (e: Exception) {
            null
        }
    }

    fun clearToken() {
        prefs.edit()
            .remove("jwt_token")
            .apply()
    }
}