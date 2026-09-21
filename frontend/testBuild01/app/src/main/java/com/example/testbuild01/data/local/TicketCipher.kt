package com.example.testbuild01.data.local

import android.content.Context
import android.util.Base64
import com.google.crypto.tink.Aead
import com.google.crypto.tink.KeyTemplates
import com.google.crypto.tink.RegistryConfiguration
import com.google.crypto.tink.aead.AeadConfig
import com.google.crypto.tink.integration.android.AndroidKeysetManager
import com.google.gson.Gson

/** 티켓 상세 정보를 암호화/복호화한다. 티켓 데이터에만 사용하며 TokenManager와는 무관하다. */
interface TicketCipher {
    fun encrypt(plainText: String): String
    fun decrypt(cipherText: String): String
}

/** 암호화 대상 상세 정보 */
data class TicketDetails(
    val confirmationNumber: String?,
    val barcodeValue: String,
    val barcodeFormat: String
)

fun TicketDetails.toJson(): String = Gson().toJson(this)

fun String.toTicketDetails(): TicketDetails = Gson().fromJson(this, TicketDetails::class.java)

/** Tink AES256-GCM. 키셋은 Android Keystore 마스터 키로 감싸 SharedPreferences에 저장한다. */
class TinkTicketCipher(context: Context) : TicketCipher {

    private val aead: Aead by lazy {
        AeadConfig.register()
        AndroidKeysetManager.Builder()
            .withSharedPref(context.applicationContext, KEYSET_NAME, PREF_FILE_NAME)
            .withKeyTemplate(KeyTemplates.get("AES256_GCM"))
            .withMasterKeyUri(MASTER_KEY_URI)
            .build()
            .keysetHandle
            .getPrimitive(RegistryConfiguration.get(), Aead::class.java)
    }

    override fun encrypt(plainText: String): String =
        Base64.encodeToString(aead.encrypt(plainText.toByteArray(Charsets.UTF_8), ASSOCIATED_DATA), Base64.NO_WRAP)

    override fun decrypt(cipherText: String): String =
        String(aead.decrypt(Base64.decode(cipherText, Base64.NO_WRAP), ASSOCIATED_DATA), Charsets.UTF_8)

    private companion object {
        const val KEYSET_NAME = "ticket_keyset"
        // backup 제외 규칙의 "ticket_keyset.xml"과 일치
        const val PREF_FILE_NAME = "ticket_keyset"
        const val MASTER_KEY_URI = "android-keystore://ticket_master_key"
        val ASSOCIATED_DATA = "ticket-details".toByteArray(Charsets.UTF_8)
    }
}
