package com.example.testbuild01.data.repository

import com.example.testbuild01.data.local.TicketCipher
import com.example.testbuild01.data.local.TicketDao
import com.example.testbuild01.data.local.TicketDetails
import com.example.testbuild01.data.local.TicketEntity
import com.example.testbuild01.data.local.TicketType
import com.example.testbuild01.data.local.toJson
import com.example.testbuild01.data.local.toTicketDetails
import com.example.testbuild01.data.model.Ticket
import com.example.testbuild01.notification.TicketAlarmScheduler
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

/** 화면에서 입력받은 티켓 값. 저장 시 암호화된 [TicketEntity]로 변환된다. */
data class TicketDraft(
    val type: TicketType,
    val title: String,
    val startDateTime: Long,
    val locationFrom: String,
    val locationTo: String,
    val barcodeValue: String = "",
    val barcodeFormat: String = "",
    val confirmationNumber: String? = null
)

/**
 * 저장/수정/삭제와 알림 예약 취소·재등록을 한 곳에서 묶어 화면이 예약을 빠뜨리지 않게 한다.
 * 바코드 값·포맷·확인번호는 DB에 평문으로 남기지 않고 encryptedDetailsJson에만 저장하며, 읽을 때 복호화한다.
 */
class TicketRepository(
    private val dao: TicketDao,
    private val cipher: TicketCipher,
    private val scheduler: TicketAlarmScheduler,
    private val now: () -> Long = { System.currentTimeMillis() }
) {
    fun observeAll(): Flow<List<Ticket>> = dao.observeAll().map { list -> list.map { it.toTicket() } }

    suspend fun getById(id: Long): Ticket? = dao.getById(id)?.toTicket()

    suspend fun add(draft: TicketDraft): Long {
        val id = dao.insert(draft.toEntity(id = 0))
        scheduler.schedule(draft.toEntity(id = id))
        return id
    }

    suspend fun update(id: Long, draft: TicketDraft) {
        val entity = draft.toEntity(id = id)
        dao.update(entity)
        scheduler.reschedule(entity)
    }

    suspend fun delete(ticket: Ticket) {
        dao.getById(ticket.id)?.let { dao.delete(it) }
        scheduler.cancel(ticket.id)
    }

    /** 재부팅이나 권한 변경 후 미래 티켓의 알림을 다시 등록한다. */
    suspend fun rescheduleUpcoming() {
        dao.getUpcoming(now()).forEach { scheduler.reschedule(it) }
    }

    private fun TicketDraft.toEntity(id: Long) = TicketEntity(
        id = id,
        type = type,
        title = title,
        startDateTime = startDateTime,
        locationFrom = locationFrom,
        locationTo = locationTo,
        // 민감 값은 평문 컬럼에 두지 않는다.
        barcodeValue = "",
        barcodeFormat = "",
        confirmationNumber = null,
        encryptedDetailsJson = cipher.encrypt(
            TicketDetails(confirmationNumber, barcodeValue, barcodeFormat).toJson()
        )
    )

    private fun TicketEntity.toTicket(): Ticket {
        // 키셋 유실(앱 데이터 복원 등)로 복호화에 실패해도 목록과 알림은 동작하도록 빈 상세 정보로 대체한다.
        val details = runCatching { cipher.decrypt(encryptedDetailsJson).toTicketDetails() }.getOrNull()
        return Ticket(
            id = id,
            type = type,
            title = title,
            startDateTime = startDateTime,
            locationFrom = locationFrom,
            locationTo = locationTo,
            barcodeValue = details?.barcodeValue.orEmpty(),
            barcodeFormat = details?.barcodeFormat.orEmpty(),
            confirmationNumber = details?.confirmationNumber
        )
    }
}
