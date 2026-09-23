@file:OptIn(ExperimentalMaterial3Api::class)

package com.example.testbuild01.ui.hotel

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.Checkbox
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TimePicker
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.material3.rememberTimePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.local.HotelDetail
import com.example.testbuild01.data.hotel.HotelOcrCandidate
import com.example.testbuild01.ui.ticket.formatDateTime
import java.util.Calendar
import java.util.TimeZone

/** [HotelDetailReviewScreen]/[HotelManualEntryScreen]이 공유하는 초기값. */
data class HotelFormSeed(
    val hotelName: String = "",
    val address: String = "",
    val latitude: Double? = null,
    val longitude: Double? = null,
    val checkInTime: Long? = null,
    val checkOutTime: Long? = null,
    val roomType: String? = null,
    val guestCount: Int? = null,
    val confirmationNumber: String = "",
    val guestNameOnBooking: String = "",
    val freeCancellationDeadline: Long? = null,
    val phoneNumber: String? = null
)

fun HotelDetail.toFormSeed() = HotelFormSeed(
    hotelName = hotelName,
    address = address,
    latitude = latitude,
    longitude = longitude,
    checkInTime = checkInTime,
    checkOutTime = checkOutTime,
    roomType = roomType,
    guestCount = guestCount,
    confirmationNumber = confirmationNumber,
    guestNameOnBooking = guestNameOnBooking,
    freeCancellationDeadline = freeCancellationDeadline,
    phoneNumber = phoneNumber
)

fun HotelOcrCandidate.toFormSeed() = HotelFormSeed(
    hotelName = hotelName,
    address = address,
    checkInTime = checkInMillis,
    checkOutTime = checkOutMillis,
    confirmationNumber = confirmationNumber,
    guestNameOnBooking = guestNameOnBooking
)

/**
 * OCR 인식 결과 확인/수정, 수동 입력, 기존 예약 수정이 모두 공유하는 폼.
 * 필수 항목이 비어 있으면 저장 버튼이 비활성화된다. confirmationNumber는 강조 테두리로 표시한다.
 */
@Composable
fun HotelDetailForm(
    title: String,
    seed: HotelFormSeed,
    onBack: () -> Unit,
    onSave: (HotelDetail) -> Unit
) {
    var hotelName by remember { mutableStateOf(seed.hotelName) }
    var address by remember { mutableStateOf(seed.address) }
    var latitudeText by remember { mutableStateOf(seed.latitude?.toString() ?: "") }
    var longitudeText by remember { mutableStateOf(seed.longitude?.toString() ?: "") }
    var checkInTime by remember { mutableStateOf(seed.checkInTime) }
    var checkOutTime by remember { mutableStateOf(seed.checkOutTime) }
    var roomType by remember { mutableStateOf(seed.roomType.orEmpty()) }
    var guestCountText by remember { mutableStateOf(seed.guestCount?.toString() ?: "") }
    var confirmationNumber by remember { mutableStateOf(seed.confirmationNumber) }
    var guestNameOnBooking by remember { mutableStateOf(seed.guestNameOnBooking) }
    var enableFreeCancellation by remember { mutableStateOf(seed.freeCancellationDeadline != null) }
    var freeCancellationDeadline by remember { mutableStateOf(seed.freeCancellationDeadline) }
    var phoneNumber by remember { mutableStateOf(seed.phoneNumber.orEmpty()) }

    val guestCount = guestCountText.toIntOrNull()
    val canSave = hotelName.isNotBlank() && address.isNotBlank() &&
        checkInTime != null && checkOutTime != null &&
        guestCount != null && guestCount > 0 &&
        confirmationNumber.isNotBlank() && guestNameOnBooking.isNotBlank()

    fun save() {
        onSave(
            HotelDetail(
                hotelName = hotelName.trim(),
                address = address.trim(),
                latitude = latitudeText.toDoubleOrNull(),
                longitude = longitudeText.toDoubleOrNull(),
                checkInTime = checkInTime!!,
                checkOutTime = checkOutTime!!,
                roomType = roomType.trim().ifEmpty { null },
                guestCount = guestCount!!,
                confirmationNumber = confirmationNumber.trim(),
                guestNameOnBooking = guestNameOnBooking.trim(),
                freeCancellationDeadline = if (enableFreeCancellation) freeCancellationDeadline else null,
                phoneNumber = phoneNumber.trim().ifEmpty { null }
            )
        )
    }

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text(title) },
                navigationIcon = { TextButton(onClick = onBack) { Text("뒤로") } }
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            OutlinedTextField(
                value = hotelName,
                onValueChange = { hotelName = it },
                label = { Text("호텔명") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )
            OutlinedTextField(
                value = address,
                onValueChange = { address = it },
                label = { Text("주소") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = latitudeText,
                    onValueChange = { latitudeText = it },
                    label = { Text("위도 (선택)") },
                    singleLine = true,
                    modifier = Modifier.weight(1f)
                )
                OutlinedTextField(
                    value = longitudeText,
                    onValueChange = { longitudeText = it },
                    label = { Text("경도 (선택)") },
                    singleLine = true,
                    modifier = Modifier.weight(1f)
                )
            }

            DateTimePickerButton(
                label = "체크인 일시",
                millis = checkInTime,
                onPicked = { checkInTime = it }
            )
            DateTimePickerButton(
                label = "체크아웃 일시",
                millis = checkOutTime,
                onPicked = { checkOutTime = it }
            )

            OutlinedTextField(
                value = roomType,
                onValueChange = { roomType = it },
                label = { Text("객실 타입 (선택)") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )
            OutlinedTextField(
                value = guestCountText,
                onValueChange = { guestCountText = it.filter(Char::isDigit) },
                label = { Text("투숙 인원") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )

            // 체크인 시 프런트 대조에 실제로 쓰이는 핵심 필드라 강조 표시한다.
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .border(BorderStroke(2.dp, MaterialTheme.colorScheme.primary), RoundedCornerShape(4.dp))
                    .padding(4.dp)
            ) {
                OutlinedTextField(
                    value = confirmationNumber,
                    onValueChange = { confirmationNumber = it },
                    label = { Text("확인번호") },
                    singleLine = true,
                    colors = OutlinedTextFieldDefaults.colors(
                        unfocusedBorderColor = MaterialTheme.colorScheme.primary
                    ),
                    modifier = Modifier.fillMaxWidth()
                )
            }
            OutlinedTextField(
                value = guestNameOnBooking,
                onValueChange = { guestNameOnBooking = it },
                label = { Text("예약자명", fontWeight = FontWeight.Bold) },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )

            Row {
                Checkbox(
                    checked = enableFreeCancellation,
                    onCheckedChange = { checked ->
                        enableFreeCancellation = checked
                        if (!checked) freeCancellationDeadline = null
                    }
                )
                Text("무료 취소 마감이 있어요", modifier = Modifier.padding(top = 12.dp))
            }
            if (enableFreeCancellation) {
                DateTimePickerButton(
                    label = "무료취소 마감 일시",
                    millis = freeCancellationDeadline,
                    onPicked = { freeCancellationDeadline = it }
                )
            }

            OutlinedTextField(
                value = phoneNumber,
                onValueChange = { phoneNumber = it },
                label = { Text("호텔 전화번호 (선택)") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )

            Button(
                enabled = canSave,
                onClick = ::save,
                modifier = Modifier.fillMaxWidth()
            ) { Text("저장") }
        }
    }
}

@Composable
private fun DateTimePickerButton(label: String, millis: Long?, onPicked: (Long) -> Unit) {
    var showDatePicker by remember { mutableStateOf(false) }
    var showTimePicker by remember { mutableStateOf(false) }
    var pickedDate by remember { mutableStateOf<Triple<Int, Int, Int>?>(null) }

    OutlinedButton(
        onClick = { showDatePicker = true },
        modifier = Modifier.fillMaxWidth()
    ) {
        Text(millis?.let { "$label: ${formatDateTime(it)}" } ?: "$label 선택")
    }

    if (showDatePicker) {
        val dateState = rememberDatePickerState(initialSelectedDateMillis = millis?.let { utcMidnightOf(it) })
        DatePickerDialog(
            onDismissRequest = { showDatePicker = false },
            confirmButton = {
                TextButton(
                    enabled = dateState.selectedDateMillis != null,
                    onClick = {
                        val utc = Calendar.getInstance(TimeZone.getTimeZone("UTC"))
                        utc.timeInMillis = dateState.selectedDateMillis!!
                        pickedDate = Triple(
                            utc.get(Calendar.YEAR),
                            utc.get(Calendar.MONTH),
                            utc.get(Calendar.DAY_OF_MONTH)
                        )
                        showDatePicker = false
                        showTimePicker = true
                    }
                ) { Text("다음") }
            },
            dismissButton = { TextButton(onClick = { showDatePicker = false }) { Text("취소") } }
        ) {
            DatePicker(state = dateState)
        }
    }

    if (showTimePicker) {
        val initial = Calendar.getInstance().apply { millis?.let { timeInMillis = it } }
        val timeState = rememberTimePickerState(
            initialHour = initial.get(Calendar.HOUR_OF_DAY),
            initialMinute = initial.get(Calendar.MINUTE),
            is24Hour = true
        )
        AlertDialog(
            onDismissRequest = { showTimePicker = false },
            confirmButton = {
                TextButton(onClick = {
                    pickedDate?.let { (year, month, day) ->
                        onPicked(
                            Calendar.getInstance().apply {
                                clear()
                                set(year, month, day, timeState.hour, timeState.minute)
                            }.timeInMillis
                        )
                    }
                    showTimePicker = false
                }) { Text("확인") }
            },
            dismissButton = { TextButton(onClick = { showTimePicker = false }) { Text("취소") } },
            text = { TimePicker(state = timeState) }
        )
    }
}

private fun utcMidnightOf(localMillis: Long): Long {
    val local = Calendar.getInstance().apply { timeInMillis = localMillis }
    return Calendar.getInstance(TimeZone.getTimeZone("UTC")).apply {
        clear()
        set(local.get(Calendar.YEAR), local.get(Calendar.MONTH), local.get(Calendar.DAY_OF_MONTH))
    }.timeInMillis
}
