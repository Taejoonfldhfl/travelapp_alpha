package com.example.testbuild01.data.model

import com.example.testbuild01.data.local.TicketType

/** 복호화된 상세 정보를 포함한 티켓. 화면과 알림은 [com.example.testbuild01.data.local.TicketEntity] 대신 이 모델을 쓴다. */
data class Ticket(
    val id: Long,
    val type: TicketType,
    val title: String,
    val startDateTime: Long,
    val locationFrom: String,
    val locationTo: String,
    /** 스캔한 원본 값. 수동 입력이거나 복호화에 실패하면 빈 문자열 */
    val barcodeValue: String,
    val barcodeFormat: String,
    val confirmationNumber: String?
)
