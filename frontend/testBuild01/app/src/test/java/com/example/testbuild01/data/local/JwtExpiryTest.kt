package com.example.testbuild01.data.local

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.Base64

class JwtExpiryTest {

    // 테스트 픽스처 조립용. java.util.Base64는 여기(JVM에서 직접 실행되는 테스트 코드)에서는
    // 안전하게 쓸 수 있다 - 문제는 Android 기기(minSdk 24)에서 JwtExpiry가 이걸 쓰는 경우였다.
    private fun fakeJwt(payloadJson: String): String {
        val header = encode("""{"alg":"HS256","typ":"JWT"}""")
        val payload = encode(payloadJson)
        return "$header.$payload.signature"
    }

    private fun encode(json: String): String =
        Base64.getUrlEncoder().withoutPadding().encodeToString(json.toByteArray(Charsets.UTF_8))

    @Test
    fun isExpired_futureExp_returnsFalse() {
        val token = fakeJwt("""{"exp":9999999999}""")

        assertFalse(JwtExpiry.isExpired(token, nowEpochSeconds = 1000))
    }

    @Test
    fun isExpired_pastExp_returnsTrue() {
        val token = fakeJwt("""{"exp":1000}""")

        assertTrue(JwtExpiry.isExpired(token, nowEpochSeconds = 2000))
    }

    @Test
    fun expiresAtEpochSeconds_koreanClaimsAlongsideExp_stillParsesExpCorrectly() {
        val token = fakeJwt("""{"exp":5000,"nickname":"홍길동","role":"여행자"}""")

        assertEquals(5000L, JwtExpiry.expiresAtEpochSeconds(token))
    }

    @Test
    fun isExpired_exactlyAtExpiryBoundary_isTreatedAsExpired() {
        val token = fakeJwt("""{"exp":5000}""")

        assertFalse(JwtExpiry.isExpired(token, nowEpochSeconds = 4999))
        assertTrue(JwtExpiry.isExpired(token, nowEpochSeconds = 5000))
    }

    @Test
    fun expiresAtEpochSeconds_tokenWithoutExpClaim_returnsNull() {
        val token = fakeJwt("""{"sub":"1"}""")

        assertNull(JwtExpiry.expiresAtEpochSeconds(token))
        assertTrue(JwtExpiry.isExpired(token))
    }

    @Test
    fun isExpired_nullToken_isTreatedAsExpired() {
        assertTrue(JwtExpiry.isExpired(null))
    }

    @Test
    fun isExpired_malformedToken_isTreatedAsExpired() {
        assertTrue(JwtExpiry.isExpired("not-a-jwt"))
        assertTrue(JwtExpiry.isExpired(""))
        assertTrue(JwtExpiry.isExpired("only-one-part"))
    }

    @Test
    fun isExpired_invalidBase64Payload_isTreatedAsExpired() {
        assertTrue(JwtExpiry.isExpired("header.@@not-base64@@.sig"))
    }
}
