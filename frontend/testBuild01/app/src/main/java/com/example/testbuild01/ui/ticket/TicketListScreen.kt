package com.example.testbuild01.ui.ticket

import android.app.AlarmManager
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.runtime.collectAsState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.LifecycleOwner
import com.example.testbuild01.data.model.Ticket

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TicketListScreen(
    viewModel: TicketViewModel,
    highlightId: Long,
    onAddTicket: () -> Unit,
    onEditTicket: (Ticket) -> Unit,
    onBack: () -> Unit
) {
    val tickets by viewModel.tickets.collectAsState()

    var viewingId by rememberSaveable { mutableStateOf<Long?>(null) }
    var deleting by remember { mutableStateOf<Ticket?>(null) }
    val viewing = tickets.firstOrNull { it.id == viewingId }

    // 알림 딥링크로 들어오면 해당 티켓의 코드를 바로 연다. 목록이 로드된 뒤 한 번만 처리한다.
    var highlightHandled by rememberSaveable(highlightId) { mutableStateOf(highlightId < 0) }
    LaunchedEffect(highlightId, tickets) {
        if (!highlightHandled && tickets.any { it.id == highlightId }) {
            viewingId = highlightId
            highlightHandled = true
        }
    }

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text("내 티켓") },
                navigationIcon = { TextButton(onClick = onBack) { Text("뒤로") } }
            )
        },
        floatingActionButton = {
            FloatingActionButton(onClick = onAddTicket) { Text("+") }
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            ExactAlarmBanner()

            if (tickets.isEmpty()) {
                Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    Text("등록된 티켓이 없습니다. + 버튼으로 스캔해 보세요.")
                }
            } else {
                LazyColumn(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(16.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    items(tickets, key = { it.id }) { ticket ->
                        TicketCard(
                            ticket = ticket,
                            onClick = { viewingId = ticket.id },
                            onEdit = { onEditTicket(ticket) },
                            onDelete = { deleting = ticket }
                        )
                    }
                }
            }
        }
    }

    viewing?.let { TicketCodeDialog(ticket = it, onDismiss = { viewingId = null }) }

    deleting?.let { ticket ->
        AlertDialog(
            onDismissRequest = { deleting = null },
            title = { Text("티켓 삭제") },
            text = { Text("\"${ticket.title}\" 티켓과 예약된 알림을 삭제할까요?") },
            confirmButton = {
                TextButton(onClick = {
                    viewModel.delete(ticket)
                    if (viewingId == ticket.id) viewingId = null
                    deleting = null
                }) { Text("삭제") }
            },
            dismissButton = { TextButton(onClick = { deleting = null }) { Text("취소") } }
        )
    }
}

@Composable
private fun TicketCard(
    ticket: Ticket,
    onClick: () -> Unit,
    onEdit: () -> Unit,
    onDelete: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
            Text(
                text = "${ticket.type.label()} · ${formatDateTime(ticket.startDateTime)}",
                style = MaterialTheme.typography.labelLarge
            )
            Text(text = ticket.title, style = MaterialTheme.typography.titleLarge)
            Text(text = "${ticket.locationFrom} → ${ticket.locationTo}")
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                TextButton(onClick = onEdit) { Text("수정") }
                TextButton(onClick = onDelete) { Text("삭제") }
            }
        }
    }
}

/** 카드 탭 시 풀스크린으로 코드를 보여준다. 다시 그릴 수 없으면 확인번호만 크게 표시한다. */
@Composable
private fun TicketCodeDialog(ticket: Ticket, onDismiss: () -> Unit) {
    val bitmap = remember(ticket.barcodeValue, ticket.barcodeFormat) {
        BarcodeRenderer.render(ticket.barcodeValue, ticket.barcodeFormat)
    }

    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false)
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .background(Color.White)
                .padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            Text(text = ticket.title, color = Color.Black, style = MaterialTheme.typography.titleLarge)
            Text(
                text = "${ticket.locationFrom} → ${ticket.locationTo}",
                color = Color.DarkGray,
                modifier = Modifier.padding(bottom = 24.dp)
            )

            if (bitmap != null) {
                Image(
                    bitmap = bitmap.asImageBitmap(),
                    contentDescription = "티켓 코드",
                    contentScale = ContentScale.Fit,
                    modifier = Modifier.fillMaxWidth()
                )
                ticket.confirmationNumber?.let {
                    Text(
                        text = "확인번호 $it",
                        color = Color.Black,
                        style = MaterialTheme.typography.titleMedium,
                        modifier = Modifier.padding(top = 16.dp)
                    )
                }
            } else if (!ticket.confirmationNumber.isNullOrBlank()) {
                Text(text = "확인번호", color = Color.DarkGray)
                Text(
                    text = ticket.confirmationNumber,
                    color = Color.Black,
                    fontSize = 40.sp,
                    fontWeight = FontWeight.Bold
                )
            } else {
                Text(text = "표시할 코드가 없습니다.", color = Color.Black)
            }

            Button(onClick = onDismiss, modifier = Modifier.padding(top = 32.dp)) { Text("닫기") }
        }
    }
}

/** 정확한 알람 권한이 없으면 알림이 지연될 수 있음을 알리고 설정 화면으로 안내한다. */
@Composable
private fun ExactAlarmBanner() {
    if (Build.VERSION.SDK_INT < Build.VERSION_CODES.S) return

    val context = LocalContext.current
    val alarmManager = remember { context.getSystemService(AlarmManager::class.java) }
    var allowed by remember { mutableStateOf(alarmManager.canScheduleExactAlarms()) }

    // 설정 화면에서 돌아왔을 때 다시 확인한다.
    val lifecycleOwner = context as LifecycleOwner
    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) allowed = alarmManager.canScheduleExactAlarms()
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    if (allowed) return

    Card(modifier = Modifier.fillMaxWidth().padding(16.dp)) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text("정확한 알람 권한이 없어 탑승 알림이 몇 분 이상 늦게 도착할 수 있습니다.")
            TextButton(onClick = {
                context.startActivity(
                    Intent(Settings.ACTION_REQUEST_SCHEDULE_EXACT_ALARM, Uri.parse("package:${context.packageName}"))
                )
            }) { Text("권한 허용하기") }
        }
    }
}
