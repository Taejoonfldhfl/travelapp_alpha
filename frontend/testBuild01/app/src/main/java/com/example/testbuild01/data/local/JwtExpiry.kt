package com.example.testbuild01.data.local

/**
 * JWT의 payload(가운데 세그먼트)에서 exp(만료 시각, epoch seconds) 클레임만 읽는다.
 * android.util.Base64는 Android 런타임에서만 동작해 JVM 유닛테스트가 불가능하고,
 * java.util.Base64는 API 26 이상에서만 보장돼(이 앱의 minSdk는 24) 기기에 따라 깨질 수 있으므로,
 * 둘 다 쓰지 않고 Base64URL 디코딩을 직접 구현한 순수 Kotlin으로 만든다.
 */
object JwtExpiry {

    private const val BASE64_URL_ALPHABET =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"

    // JSON 파서 없이, "exp":<숫자> 형태의 클레임만 정규식으로 뽑는다. 다른 클레임에 한글 등
    // 멀티바이트 문자가 섞여 있어도 UTF-8로 디코딩한 문자열에서 숫자 패턴만 찾으므로 영향 없다.
    private val EXP_CLAIM_REGEX = Regex(""""exp"\s*:\s*(\d+)""")

    /** exp 클레임(epoch seconds)을 읽는다. 토큰이 null이거나 형식이 깨져 있거나 exp가 없으면 null. */
    fun expiresAtEpochSeconds(token: String?): Long? {
        if (token.isNullOrEmpty()) return null
        val payload = token.split(".").getOrNull(1) ?: return null
        val json = try {
            String(decodeBase64Url(payload), Charsets.UTF_8)
        } catch (e: Exception) {
            return null
        }
        return EXP_CLAIM_REGEX.find(json)?.groupValues?.get(1)?.toLongOrNull()
    }

    /** exp가 없거나 이미 지났으면(경계 포함) true로 취급한다 — 읽지 못하는 토큰은 만료로 간주. */
    fun isExpired(token: String?, nowEpochSeconds: Long = System.currentTimeMillis() / 1000): Boolean {
        val exp = expiresAtEpochSeconds(token) ?: return true
        return exp <= nowEpochSeconds
    }

    private fun decodeBase64Url(input: String): ByteArray {
        val clean = input.trimEnd('=')
        val out = java.io.ByteArrayOutputStream()
        var buffer = 0
        var bitsCollected = 0
        for (c in clean) {
            val value = BASE64_URL_ALPHABET.indexOf(c)
            if (value < 0) throw IllegalArgumentException("올바르지 않은 Base64URL 문자: $c")
            buffer = (buffer shl 6) or value
            bitsCollected += 6
            if (bitsCollected >= 8) {
                bitsCollected -= 8
                out.write((buffer shr bitsCollected) and 0xFF)
            }
        }
        return out.toByteArray()
    }
}
