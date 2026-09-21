package com.example.testbuild01.notification

import com.example.testbuild01.data.local.TicketEntity

/** 실제 예약 수단(AlarmManager / WorkManager)을 감싸는 추상화. 스케줄링 로직을 JVM에서 테스트하기 위해 분리했다. */
interface AlarmBackend {
    /** SCHEDULE_EXACT_ALARM 권한(Android 12+)이 있어 정확한 알람을 걸 수 있는지 */
    fun canScheduleExact(): Boolean
    fun scheduleExact(ticketId: Long, triggerAtMillis: Long)
    fun scheduleFallback(ticketId: Long, triggerAtMillis: Long)
    /** 정확한 알람과 폴백 작업을 모두 취소한다. 예약이 없어도 안전해야 한다. */
    fun cancel(ticketId: Long)
}

/**
 * 티켓 알림 예약 정책.
 * - 정확한 알람 권한이 있으면 AlarmManager.setExactAndAllowWhileIdle
 * - 없으면 WorkManager로 폴백 (지연될 수 있음)
 */
class TicketAlarmScheduler(
    private val backend: AlarmBackend,
    private val now: () -> Long = { System.currentTimeMillis() }
) {
    enum class Result { EXACT, FALLBACK, SKIPPED }

    fun schedule(ticket: TicketEntity): Result {
        // 권한 상태가 바뀐 경우를 포함해 이전 예약이 남지 않도록 항상 먼저 지운다.
        backend.cancel(ticket.id)

        val triggerAt = TicketAlertConfig.alertTimeMillis(ticket.startDateTime)
        if (triggerAt <= now()) return Result.SKIPPED

        return if (backend.canScheduleExact()) {
            backend.scheduleExact(ticket.id, triggerAt)
            Result.EXACT
        } else {
            backend.scheduleFallback(ticket.id, triggerAt)
            Result.FALLBACK
        }
    }

    fun cancel(ticketId: Long) = backend.cancel(ticketId)

    /** 시간 수정 시 호출. 취소 후 새 시각으로 다시 등록한다. */
    fun reschedule(ticket: TicketEntity): Result = schedule(ticket)
}
