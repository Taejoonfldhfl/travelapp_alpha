package com.example.testbuild01.data.repository

import com.example.testbuild01.data.local.HotelDetail
import com.example.testbuild01.data.model.ScheduleCreateRequest
import com.example.testbuild01.data.model.ScheduleResponse
import com.example.testbuild01.data.model.ScheduleUpdateRequest
import com.example.testbuild01.data.model.TripResponse
import java.text.SimpleDateFormat
import java.util.Locale
import java.util.TimeZone

/**
 * 호텔 예약 → 여행 일정 변환. Schedule은 여행 멤버 전원이 보는 공유 데이터이므로
 * 이름·주소·좌표·체크인/체크아웃 시각만 넘긴다. confirmationNumber/guestNameOnBooking은 여기서 절대 참조하지 않는다.
 */
object HotelScheduleMapper {
    // 서버 DTO의 StringLength 제한
    private const val TITLE_MAX = 50
    private const val PLACE_NAME_MAX = 100

    fun toCreateRequest(hotel: HotelDetail, zone: TimeZone): ScheduleCreateRequest = ScheduleCreateRequest(
        title = hotel.hotelName.take(TITLE_MAX),
        placeName = hotel.address.take(PLACE_NAME_MAX),
        description = "",
        startTime = formatServerDateTime(hotel.checkInTime, zone),
        endTime = formatServerDateTime(hotel.checkOutTime, zone),
        order = 0,
        latitude = hotel.latitude,
        longitude = hotel.longitude,
        isHotelCheckIn = true
    )

    /**
     * 서버 PUT은 전체 덮어쓰기라서, 호텔에서 온 값만 바꾸고 나머지(설명·순서·우선순위 등 다른 멤버가 고쳤을 수 있는 값)는
     * 현재 서버 값을 그대로 돌려보낸다. 로컬 좌표가 없으면 서버 좌표를 지우지 않는다.
     */
    fun toUpdateRequest(current: ScheduleResponse, hotel: HotelDetail, zone: TimeZone): ScheduleUpdateRequest {
        val hasCoordinates = hotel.latitude != null && hotel.longitude != null
        return ScheduleUpdateRequest(
            title = hotel.hotelName.take(TITLE_MAX),
            placeName = hotel.address.take(PLACE_NAME_MAX),
            description = current.description,
            startTime = formatServerDateTime(hotel.checkInTime, zone),
            endTime = formatServerDateTime(hotel.checkOutTime, zone),
            order = current.order,
            latitude = if (hasCoordinates) hotel.latitude else current.latitude,
            longitude = if (hasCoordinates) hotel.longitude else current.longitude,
            priority = current.priority,
            isEssential = current.isEssential,
            isHotelCheckIn = current.isHotelCheckIn
        )
    }

    /**
     * 이 요청으로 만들어졌을(또는 같은 호텔로 이미 만들어진) 일정인지. 서버 DateTime 직렬화에 소수초나 오프셋이
     * 붙을 수 있어 앞 19자(yyyy-MM-ddTHH:mm:ss)만 비교한다.
     */
    fun isSameHotelSchedule(schedule: ScheduleResponse, request: ScheduleCreateRequest): Boolean =
        schedule.isHotelCheckIn &&
            schedule.title == request.title &&
            schedule.startTime.take(19) == request.startTime.take(19) &&
            schedule.endTime.take(19) == request.endTime.take(19)

    /** 서버 일정에 반영되는 값이 바뀌었을 때만 "일정도 수정할까요?"를 묻는다. */
    fun scheduleFieldsChanged(before: HotelDetail?, after: HotelDetail?): Boolean {
        if (before == null || after == null) return before != after
        return before.hotelName != after.hotelName ||
            before.address != after.address ||
            before.latitude != after.latitude ||
            before.longitude != after.longitude ||
            before.checkInTime != after.checkInTime ||
            before.checkOutTime != after.checkOutTime
    }

    /** 서버는 여행 기간 밖의 일정을 400으로 거부하므로, 선택 목록에서 미리 걸러낸다(날짜 단위 비교). */
    fun fitsTrip(hotel: HotelDetail, trip: TripResponse, zone: TimeZone): Boolean {
        val tripStart = trip.startDate.take(10)
        val tripEnd = trip.endDate.take(10)
        return formatDate(hotel.checkInTime, zone) >= tripStart && formatDate(hotel.checkOutTime, zone) <= tripEnd
    }

    /** 서버 DateTime(오프셋 없음)과 같은 형식. 일정 화면에서 직접 입력하는 값과 같은 로컬 시각 기준이다. */
    fun formatServerDateTime(epochMillis: Long, zone: TimeZone): String =
        SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.US).apply { timeZone = zone }.format(epochMillis)

    fun formatDate(epochMillis: Long, zone: TimeZone): String =
        SimpleDateFormat("yyyy-MM-dd", Locale.US).apply { timeZone = zone }.format(epochMillis)
}
