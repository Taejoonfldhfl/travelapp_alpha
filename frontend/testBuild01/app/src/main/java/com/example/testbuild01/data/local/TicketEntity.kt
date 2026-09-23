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
    val encryptedDetailsJson: String,
    /**
     * 두 번째 알림 시각(평문). 호텔의 무료취소 마감 알림에만 쓰이며 BUS/FLIGHT는 항상 null이다.
     * [TicketAlarmScheduler]가 복호화 없이 바로 두 번째 알람을 예약할 수 있도록 평문 컬럼으로 둔다.
     */
    val secondaryAlertAt: Long? = null,
    /**
     * 사용자가 명시적으로 "일정에 반영"한 호텔 티켓이 가리키는 서버 Schedule/Trip ID(평문).
     * 서버 ID일 뿐 개인정보가 아니며, 복호화에 실패해도 연결 정보는 남도록 암호화 JSON 밖에 둔다.
     * 수정·삭제 API 경로에 tripId가 함께 필요하므로 두 값은 항상 같이 채워지거나 같이 null이다. BUS/FLIGHT는 항상 null.
     */
    val linkedScheduleId: Int? = null,
    val linkedTripId: Int? = null
)
