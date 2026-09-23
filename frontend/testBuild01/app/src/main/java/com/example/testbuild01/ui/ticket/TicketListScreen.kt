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
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExtendedFloatingActionButton
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
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.LifecycleOwner
import com.example.testbuild01.data.local.TicketType
import com.example.testbuild01.data.model.Ticket
import com.example.testbuild01.ui.hotel.HotelLocationSection
import com.example.testbuild01.ui.hotel.HotelScheduleLinkDialogHost

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TicketListScreen(
    viewModel: TicketViewModel,
    highlightId: Long,
    onAddTicket: () -> Unit,
    onAddHotel: () -> Unit = {},
    onSearchHotels: () -> Unit = {},
    onEditTicket: (Ticket) -> Unit,
    onBack: () -> Unit
) {
    val tickets by viewModel.tickets.collectAsState()

    var viewingId by rememberSaveable { mutableStateOf<Long?>(null) }
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
                navigationIcon = { TextButton(onClick = onBack) { Text("뒤로") } },
                actions = { TextButton(onClick = onSearchHotels) { Text("호텔 검색") } }
            )
        },
        floatingActionButton = {
            Column(horizontalAlignment = Alignment.End, verticalArrangement = Arrangement.spacedBy(8.dp)) {
                ExtendedFloatingActionButton(onClick = onAddHotel, text = { Text("호텔 등록") }, icon = { Text("+") })
                FloatingActionButton(onClick = onAddTicket) { Text("+") }
            }
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
                            onDelete = {
                                if (viewingId == ticket.id) viewingId = null
                                viewModel.requestDelete(ticket)
                            },
                            onScheduleLink = { viewModel.openTripPicker(ticket.id, askFirst = false) }
                        )
                    }
                }
            }
        }
    }

    // 호텔은 카드 자체에서 확인번호/QR을 보여주므로 풀스크린 다이얼로그 자동 오픈 대상에서 제외한다.
    viewing?.takeIf { it.type != TicketType.HOTEL }?.let { TicketCodeDialog(ticket = it, onDismiss = { viewingId = null }) }

    // 삭제 확인, 호텔 등록 직후 "일정에 반영할까요?", 수정 후 일정 갱신 확인 등을 모두 여기서 띄운다.
    HotelScheduleLinkDialogHost(viewModel)
}

@Composable
private fun TicketCard(
    ticket: Ticket,
    onClick: () -> Unit,
    onEdit: () -> Unit,
    onDelete: () -> Unit,
    onScheduleLink: () -> Unit
) {
    // 호텔은 탭→바코드 풀스크린이 아니라 카드 안에 확인번호를 바로 보여준다.
    val cardModifier = Modifier.fillMaxWidth().let {
        if (ticket.type == TicketType.HOTEL) it else it.clickable(onClick = onClick)
    }
    Card(modifier = cardModifier) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
            if (ticket.type == TicketType.HOTEL) {
                HotelCardBody(ticket)
            } else {
                Text(
                    text = "${ticket.type.label()} · ${formatDateTime(ticket.startDateTime)}",
                    style = MaterialTheme.typography.labelLarge
                )
                Text(text = ticket.title, style = MaterialTheme.typography.titleLarge)
                Text(text = "${ticket.locationFrom} → ${ticket.locationTo}")
            }
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                if (ticket.type == TicketType.HOTEL) {
                    TextButton(onClick = onScheduleLink) {
                        Text(if (ticket.isLinkedToSchedule) "일정 연결 관리" else "일정에 반영")
                    }
                }
                TextButton(onClick = onEdit) { Text("수정") }
                TextButton(onClick = onDelete) { Text("삭제") }
            }
        }
    }
}

/** 확인번호를 크게 표시 + 복사 버튼이 기본이고, barcodeValue가 있을 때만(부킹닷컴류) QR 보기를 노출한다. */
@Composable
private fun HotelCardBody(ticket: Ticket) {
    val clipboardManager = LocalClipboardManager.current
    var showBarcode by remember { mutableStateOf(false) }

    Text(
        text = "호텔 · ${formatDateTime(ticket.startDateTime)} 체크인",
        style = MaterialTheme.typography.labelLarge
    )
    Text(text = ticket.title, style = MaterialTheme.typography.titleLarge)
    Text(text = ticket.locationFrom)
    if (ticket.isLinkedToSchedule) {
        Text(
            text = "📅 여행 일정에 연결됨",
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.primary
        )
    }

    val confirmationNumber = ticket.confirmationNumber
    if (!confirmationNumber.isNullOrBlank()) {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
            Text(
                text = "확인번호 $confirmationNumber",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Bold
            )
            TextButton(onClick = { clipboardManager.setText(AnnotatedString(confirmationNumber)) }) {
                Text("복사")
            }
        }
    }

    if (ticket.barcodeValue.isNotBlank()) {
        TextButton(onClick = { showBarcode = true }) { Text("QR/바코드 보기") }
    }

    ticket.hotel?.let { HotelLocationSection(it) }

    if (showBarcode) {
        TicketCodeDialog(ticket = ticket, onDismiss = { showBarcode = false })
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
