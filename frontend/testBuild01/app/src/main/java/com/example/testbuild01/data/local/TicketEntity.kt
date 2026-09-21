package com.example.testbuild01.data.local

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "tickets")
data class TicketEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val type: TicketType,
    val title: String,
    /** 출발 일시 (epoch millis) */
    val startDateTime: Long,
    val locationFrom: String,
    val locationTo: String,
    // 아래 세 컬럼은 스키마 호환을 위해 남겨 두었지만 평문을 저장하지 않는다(항상 빈 값).
    // 실제 값은 encryptedDetailsJson 안에 있으며 TicketRepository가 복호화해서 돌려준다.
    val barcodeValue: String,
    val barcodeFormat: String,
    val confirmationNumber: String?,
    /** [TicketDetails]를 JSON으로 직렬화한 뒤 [TicketCipher]로 암호화한 값 */
    val encryptedDetailsJson: String
)
