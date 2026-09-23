package com.example.testbuild01.ui.hotel

import android.widget.Toast
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.model.Ticket
import com.example.testbuild01.data.network.SyncError
import com.example.testbuild01.data.network.SyncErrorKind
import com.example.testbuild01.data.repository.TripChoice
import com.example.testbuild01.ui.ticket.TicketViewModel

/** 호텔 티켓 ↔ 여행 일정 연동 중 목록 화면 위에 띄우는 다이얼로그 */
sealed interface HotelLinkDialog {
    /** askFirst=true면 "반영할까요?"부터, false면 바로 여행 선택 목록을 보여준다. */
    data class PickTrip(val ticketId: Long, val askFirst: Boolean, val trips: TripsState) : HotelLinkDialog
    /** 이미 연결된 티켓. 중복 생성을 막기 위해 새로 반영하는 대신 해제/갱신만 제공한다. */
    data class ManageLink(val ticket: Ticket, val tripTitle: String?) : HotelLinkDialog
    data class ConfirmSyncAfterEdit(val ticketId: Long) : HotelLinkDialog
    data class ConfirmDelete(val ticket: Ticket, val error: SyncError? = null) : HotelLinkDialog
    /** 일정이 사라졌거나(404) 여행 멤버가 아닌(403) 경우. 재시도 대신 연결 해제를 제안한다. */
    data class SyncFailed(val ticketId: Long, val error: SyncError) : HotelLinkDialog
}

sealed interface TripsState {
    data object Loading : TripsState
    data class Loaded(val choices: List<TripChoice>) : TripsState
    data class Error(val error: SyncError) : TripsState
}

fun SyncError.toUserMessage(): String = when (kind) {
    SyncErrorKind.NETWORK -> "서버에 연결하지 못했습니다."
    SyncErrorKind.UNAUTHORIZED -> "로그인이 만료되었습니다. 다시 로그인한 뒤 시도해 주세요."
    SyncErrorKind.FORBIDDEN -> "이 여행의 멤버가 아니어서 일정을 변경할 수 없습니다."
    SyncErrorKind.NOT_FOUND -> "연결된 일정을 서버에서 찾을 수 없습니다. 이미 삭제되었을 수 있어요."
    SyncErrorKind.REJECTED -> serverMessage ?: "서버가 요청을 거부했습니다."
    SyncErrorKind.SERVER -> "서버 오류가 발생했습니다. 잠시 후 다시 시도해 주세요."
    SyncErrorKind.INVALID_RESPONSE -> "서버 응답을 처리하지 못했습니다. 앱을 최신 버전으로 업데이트한 뒤 다시 시도해 주세요."
}

@Composable
fun HotelScheduleLinkDialogHost(viewModel: TicketViewModel) {
    val context = LocalContext.current
    val message = viewModel.linkMessage
    LaunchedEffect(message) {
        if (message != null) {
            Toast.makeText(context, message, Toast.LENGTH_LONG).show()
            viewModel.consumeLinkMessage()
        }
    }

    val busy = viewModel.linkBusy
    when (val dialog = viewModel.linkDialog) {
        null -> Unit
        is HotelLinkDialog.PickTrip -> PickTripDialog(dialog, busy, viewModel)
        is HotelLinkDialog.ManageLink -> ManageLinkDialog(dialog, busy, viewModel)
        is HotelLinkDialog.ConfirmSyncAfterEdit -> AlertDialog(
            onDismissRequest = {},
            title = { Text("여행 일정도 수정할까요?") },
            text = {
                Text(
                    "이 호텔은 여행 일정에 연결되어 있습니다. 바뀐 체크인·체크아웃 시각과 호텔 이름·주소·위치를 " +
                        "일정에도 반영할까요?\n\n수정하지 않으면 티켓과 여행 일정의 내용이 서로 달라질 수 있습니다."
                )
            },
            confirmButton = {
                TextButton(enabled = !busy, onClick = { viewModel.respondSyncAfterEdit(accept = true) }) { Text("일정도 수정") }
            },
            dismissButton = {
                TextButton(enabled = !busy, onClick = { viewModel.respondSyncAfterEdit(accept = false) }) { Text("티켓만 수정") }
            }
        )
        is HotelLinkDialog.ConfirmDelete -> ConfirmDeleteDialog(dialog, busy, viewModel)
        is HotelLinkDialog.SyncFailed -> AlertDialog(
            onDismissRequest = viewModel::dismissLinkDialog,
            title = { Text("일정을 수정하지 못했습니다") },
            text = {
                Text(
                    dialog.error.toUserMessage() +
                        "\n\n티켓 내용은 기기에 저장되어 있습니다. 연결을 해제하면 나중에 다른 여행에 다시 반영할 수 있어요."
                )
            },
            confirmButton = {
                TextButton(enabled = !busy, onClick = { viewModel.unlink(deleteSchedule = false) }) { Text("연결 해제") }
            },
            dismissButton = { TextButton(onClick = viewModel::dismissLinkDialog) { Text("닫기") } }
        )
    }
}

@Composable
private fun PickTripDialog(dialog: HotelLinkDialog.PickTrip, busy: Boolean, viewModel: TicketViewModel) {
    val trips = dialog.trips
    // 반영할 수 없는 사유. null이면 반영 가능.
    val blockedReason: String? = when (trips) {
        TripsState.Loading -> "참여 중인 여행 목록을 불러오는 중입니다…"
        is TripsState.Error -> trips.error.toUserMessage()
        is TripsState.Loaded -> when {
            trips.choices.isEmpty() -> "아직 참여 중인 여행이 없어 일정에 반영할 수 없습니다. 여행을 먼저 만들어 주세요."
            trips.choices.none { it.fitsStay } -> "숙박 기간을 포함하는 여행이 없습니다. 여행 기간 밖의 일정은 등록할 수 없어요."
            else -> null
        }
    }

    if (dialog.askFirst) {
        AlertDialog(
            onDismissRequest = viewModel::dismissLinkDialog,
            title = { Text("여행 일정에 반영할까요?") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("이 호텔 체크인·체크아웃을 여행 일정에 반영할까요?")
                    Text(
                        "여행 멤버 모두가 보는 일정에는 호텔 이름·주소·위치·시간만 올라가며, 확인번호와 예약자명은 공유되지 않습니다.",
                        style = MaterialTheme.typography.bodySmall
                    )
                    blockedReason?.let { Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall) }
                }
            },
            confirmButton = {
                TextButton(enabled = blockedReason == null, onClick = viewModel::proceedToTripList) { Text("반영") }
            },
            dismissButton = {
                Column {
                    if (trips is TripsState.Error) TextButton(onClick = viewModel::retryLoadTrips) { Text("다시 시도") }
                    TextButton(onClick = viewModel::dismissLinkDialog) { Text("건너뛰기") }
                }
            }
        )
        return
    }

    AlertDialog(
        onDismissRequest = viewModel::dismissLinkDialog,
        title = { Text("반영할 여행 선택") },
        text = {
            Column(
                modifier = Modifier.heightIn(max = 360.dp).verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(4.dp)
            ) {
                blockedReason?.let { Text(it) }
                if (trips is TripsState.Loaded) {
                    trips.choices.forEach { choice ->
                        TripRow(choice, enabled = choice.fitsStay && !busy, onClick = { viewModel.chooseTrip(choice.trip.id) })
                        HorizontalDivider()
                    }
                }
            }
        },
        confirmButton = {
            if (trips is TripsState.Error) TextButton(onClick = viewModel::retryLoadTrips) { Text("다시 시도") }
        },
        dismissButton = { TextButton(enabled = !busy, onClick = viewModel::dismissLinkDialog) { Text("취소") } }
    )
}

@Composable
private fun TripRow(choice: TripChoice, enabled: Boolean, onClick: () -> Unit) {
    val alpha = if (choice.fitsStay) 1f else 0.4f
    val color = MaterialTheme.colorScheme.onSurface.copy(alpha = alpha)
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(enabled = enabled, onClick = onClick)
            .padding(vertical = 8.dp)
    ) {
        Text(choice.trip.title, style = MaterialTheme.typography.titleMedium, color = color)
        Text("${choice.trip.startDate.take(10)} ~ ${choice.trip.endDate.take(10)}", style = MaterialTheme.typography.bodySmall, color = color)
        if (!choice.fitsStay) {
            Text("숙박 기간이 이 여행 기간을 벗어납니다", style = MaterialTheme.typography.bodySmall, color = color)
        }
    }
}

@Composable
private fun ManageLinkDialog(dialog: HotelLinkDialog.ManageLink, busy: Boolean, viewModel: TicketViewModel) {
    val tripLabel = dialog.tripTitle?.let { "'$it'" } ?: "여행 #${dialog.ticket.linkedTripId}"
    AlertDialog(
        onDismissRequest = viewModel::dismissLinkDialog,
        title = { Text("여행 일정에 연결됨") },
        text = {
            Text(
                "이 호텔은 이미 $tripLabel 일정에 반영되어 있습니다. 같은 일정이 두 번 만들어지지 않도록, " +
                    "다른 여행에 반영하려면 먼저 연결을 해제해 주세요."
            )
        },
        confirmButton = {
            Column(horizontalAlignment = Alignment.End) {
                TextButton(enabled = !busy, onClick = viewModel::syncNow) { Text("현재 호텔 정보로 일정 갱신") }
                TextButton(enabled = !busy, onClick = { viewModel.unlink(deleteSchedule = false) }) { Text("연결만 해제 (일정 유지)") }
                TextButton(enabled = !busy, onClick = { viewModel.unlink(deleteSchedule = true) }) {
                    Text("일정 삭제 후 연결 해제", color = MaterialTheme.colorScheme.error)
                }
                TextButton(enabled = !busy, onClick = viewModel::dismissLinkDialog) { Text("닫기") }
            }
        }
    )
}

@Composable
private fun ConfirmDeleteDialog(dialog: HotelLinkDialog.ConfirmDelete, busy: Boolean, viewModel: TicketViewModel) {
    val ticket = dialog.ticket
    if (!ticket.isLinkedToSchedule) {
        AlertDialog(
            onDismissRequest = viewModel::dismissLinkDialog,
            title = { Text("티켓 삭제") },
            text = { Text("\"${ticket.title}\" 티켓과 예약된 알림을 삭제할까요?") },
            confirmButton = {
                TextButton(enabled = !busy, onClick = { viewModel.confirmDelete(deleteSchedule = false) }) { Text("삭제") }
            },
            dismissButton = { TextButton(onClick = viewModel::dismissLinkDialog) { Text("취소") } }
        )
        return
    }

    AlertDialog(
        onDismissRequest = viewModel::dismissLinkDialog,
        title = { Text("티켓 삭제") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("\"${ticket.title}\"은(는) 여행 일정에 연결되어 있습니다. 연결된 일정도 함께 삭제할까요?")
                Text(
                    "일정은 여행 멤버 모두에게서 사라집니다. '티켓만 삭제'를 고르면 일정은 그대로 남습니다.",
                    style = MaterialTheme.typography.bodySmall
                )
                dialog.error?.let {
                    Text(
                        it.toUserMessage() + " 티켓은 삭제하지 않았습니다.",
                        color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        },
        confirmButton = {
            Column(horizontalAlignment = Alignment.End) {
                TextButton(enabled = !busy, onClick = { viewModel.confirmDelete(deleteSchedule = true) }) {
                    Text("티켓과 일정 모두 삭제", color = MaterialTheme.colorScheme.error)
                }
                TextButton(enabled = !busy, onClick = { viewModel.confirmDelete(deleteSchedule = false) }) { Text("티켓만 삭제") }
                TextButton(enabled = !busy, onClick = viewModel::dismissLinkDialog) { Text("취소") }
            }
        }
    )
}
