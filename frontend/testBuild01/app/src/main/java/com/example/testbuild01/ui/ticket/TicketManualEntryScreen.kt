package com.example.testbuild01.ui.ticket

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import com.example.testbuild01.data.local.TicketType
import com.example.testbuild01.data.repository.TicketDraft
import java.util.Calendar
import java.util.TimeZone

private const val SCAN_PREVIEW_LIMIT = 500

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TicketManualEntryScreen(
    viewModel: TicketViewModel,
    onBack: () -> Unit,
    onSaved: () -> Unit
) {
    val context = LocalContext.current
    val editing = viewModel.editingTicket
    val scan = viewModel.scanResult

    var type by remember { mutableStateOf(editing?.type ?: TicketType.FLIGHT) }
    var title by remember { mutableStateOf(editing?.title ?: "") }
    var startMillis by remember { mutableStateOf(editing?.startDateTime) }
    var from by remember { mutableStateOf(editing?.locationFrom ?: "") }
    var to by remember { mutableStateOf(editing?.locationTo ?: "") }
    var confirmation by remember { mutableStateOf(editing?.confirmationNumber ?: "") }

    // 스캔 결과가 있으면 그것을, 수정 중이면 기존 바코드를 유지한다.
    val barcodeValue = scan?.value ?: editing?.barcodeValue ?: ""
    val barcodeFormat = scan?.format ?: editing?.barcodeFormat ?: ""

    var showDatePicker by remember { mutableStateOf(false) }
    var showTimePicker by remember { mutableStateOf(false) }
    var pickedDate by remember { mutableStateOf<Triple<Int, Int, Int>?>(null) }
    var saving by remember { mutableStateOf(false) }

    val canSave = !saving && title.isNotBlank() && startMillis != null &&
        from.isNotBlank() && to.isNotBlank()

    fun save() {
        saving = true
        viewModel.save(
            TicketDraft(
                type = type,
                title = title.trim(),
                startDateTime = startMillis!!,
                locationFrom = from.trim(),
                locationTo = to.trim(),
                barcodeValue = barcodeValue,
                barcodeFormat = barcodeFormat,
                confirmationNumber = confirmation.trim().ifEmpty { null }
            ),
            onSaved
        )
    }

    // Android 13+에서는 알림 권한을 첫 저장 시점에 요청한다. 거부해도 저장은 진행한다.
    val notificationLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { save() }

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text(if (editing != null) "티켓 수정" else "티켓 등록") },
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
            if (scan != null) {
                Text(
                    text = "스캔 완료 (${scan.format}). 나머지 정보를 입력해 주세요.",
                    style = MaterialTheme.typography.bodyMedium
                )
                // 인식된 원본 값을 그대로 보여준다. 값이 이상하면 여기서 바로 확인할 수 있다.
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(modifier = Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        Text(
                            text = "인식된 값 (${scan.value.length}자)",
                            style = MaterialTheme.typography.labelLarge
                        )
                        SelectionContainer {
                            Text(
                                text = scan.value.take(SCAN_PREVIEW_LIMIT) +
                                    if (scan.value.length > SCAN_PREVIEW_LIMIT) "…" else "",
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                    }
                }
            }

            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                TicketType.entries.forEach { option ->
                    FilterChip(
                        selected = type == option,
                        onClick = { type = option },
                        label = { Text(option.label()) }
                    )
                }
            }

            OutlinedTextField(
                value = title,
                onValueChange = { title = it },
                label = { Text("제목") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )

            OutlinedButton(
                onClick = { showDatePicker = true },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(startMillis?.let { formatDateTime(it) } ?: "출발 일시 선택")
            }

            OutlinedTextField(
                value = from,
                onValueChange = { from = it },
                label = { Text("출발지") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )
            OutlinedTextField(
                value = to,
                onValueChange = { to = it },
                label = { Text("도착지") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )
            OutlinedTextField(
                value = confirmation,
                onValueChange = { confirmation = it },
                label = { Text("확인번호 (선택)") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )

            Button(
                enabled = canSave,
                onClick = {
                    val needsPermission = Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
                        ContextCompat.checkSelfPermission(
                            context, Manifest.permission.POST_NOTIFICATIONS
                        ) != PackageManager.PERMISSION_GRANTED
                    if (needsPermission) {
                        notificationLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
                    } else {
                        save()
                    }
                },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("저장")
            }
        }
    }

    if (showDatePicker) {
        val dateState = rememberDatePickerState(initialSelectedDateMillis = startMillis?.let { utcMidnightOf(it) })
        DatePickerDialog(
            onDismissRequest = { showDatePicker = false },
            confirmButton = {
                TextButton(
                    enabled = dateState.selectedDateMillis != null,
                    onClick = {
                        // DatePicker는 UTC 자정 기준 값을 돌려준다.
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
        val initial = Calendar.getInstance().apply { startMillis?.let { timeInMillis = it } }
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
                        startMillis = Calendar.getInstance().apply {
                            clear()
                            set(year, month, day, timeState.hour, timeState.minute)
                        }.timeInMillis
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
