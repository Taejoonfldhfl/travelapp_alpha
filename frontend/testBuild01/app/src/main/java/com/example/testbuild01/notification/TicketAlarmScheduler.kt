package com.example.testbuild01.notification

import com.example.testbuild01.data.local.TicketEntity

/** 티켓 하나가 가질 수 있는 알림 슬롯. 호텔만 두 슬롯을 모두 쓰고, 나머지 타입은 PRIMARY만 쓴다. */
enum class AlarmSlot { PRIMARY, SECONDARY }

/** 실제 예약 수단(AlarmManager / WorkManager)을 감싸는 추상화. 스케줄링 로직을 JVM에서 테스트하기 위해 분리했다. */
interface AlarmBackend {
    /** SCHEDULE_EXACT_ALARM 권한(Android 12+)이 있어 정확한 알람을 걸 수 있는지 */
    fun canScheduleExact(): Boolean
    fun scheduleExact(ticketId: Long, slot: AlarmSlot, triggerAtMillis: Long)
    fun scheduleFallback(ticketId: Long, slot: AlarmSlot, triggerAtMillis: Long)
    /** 정확한 알람과 폴백 작업을 모두 취소한다. 예약이 없어도 안전해야 한다. */
    fun cancel(ticketId: Long, slot: AlarmSlot)
}

/**
 * 티켓 알림 예약 정책.
 * - 정확한 알람 권한이 있으면 AlarmManager.setExactAndAllowWhileIdle
 * - 없으면 WorkManager로 폴백 (지연될 수 있음)
 * PRIMARY는 모든 타입 공통(체크인/탑승/승차 임박), SECONDARY는 호텔의 무료취소 마감 알림 전용이며
 * secondaryAlertAt이 없으면 예약하지 않는다.
 */
class TicketAlarmScheduler(
    private val backend: AlarmBackend,
    private val now: () -> Long = { System.currentTimeMillis() }
) {
    enum class Result { EXACT, FALLBACK, SKIPPED }

    /** 반환값은 PRIMARY 알림 기준이다. SECONDARY는 항상 함께 예약/취소되지만 결과에 별도로 노출하지 않는다. */
    fun schedule(ticket: TicketEntity): Result {
        val primaryResult = scheduleSlot(
            ticket.id,
            AlarmSlot.PRIMARY,
            TicketAlertConfig.alertTimeMillis(ticket.startDateTime)
        )

        val secondaryAt = ticket.secondaryAlertAt
        if (secondaryAt != null) {
            scheduleSlot(ticket.id, AlarmSlot.SECONDARY, TicketAlertConfig.alertTimeMillis(secondaryAt))
        } else {
            backend.cancel(ticket.id, AlarmSlot.SECONDARY)
        }

        return primaryResult
    }

    private fun scheduleSlot(ticketId: Long, slot: AlarmSlot, triggerAt: Long): Result {
        // 권한 상태가 바뀐 경우를 포함해 이전 예약이 남지 않도록 항상 먼저 지운다.
        backend.cancel(ticketId, slot)

        if (triggerAt <= now()) return Result.SKIPPED

        return if (backend.canScheduleExact()) {
            backend.scheduleExact(ticketId, slot, triggerAt)
            Result.EXACT
        } else {
            backend.scheduleFallback(ticketId, slot, triggerAt)
            Result.FALLBACK
        }
    }

    fun cancel(ticketId: Long) {
        backend.cancel(ticketId, AlarmSlot.PRIMARY)
        backend.cancel(ticketId, AlarmSlot.SECONDARY)
    }

    /** 시간 수정 시 호출. 취소 후 새 시각으로 다시 등록한다. */
    fun reschedule(ticket: TicketEntity): Result = schedule(ticket)
}
