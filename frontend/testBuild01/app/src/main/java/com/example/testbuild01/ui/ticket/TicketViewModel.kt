package com.example.testbuild01.ui.ticket

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider.AndroidViewModelFactory.Companion.APPLICATION_KEY
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import com.example.testbuild01.MainApplication
import com.example.testbuild01.data.hotel.HotelOcrCandidate
import com.example.testbuild01.data.local.TicketType
import com.example.testbuild01.data.model.Ticket
import com.example.testbuild01.data.network.RemoteResult
import com.example.testbuild01.data.network.SyncErrorKind
import com.example.testbuild01.data.repository.HotelScheduleLinker
import com.example.testbuild01.data.repository.LinkOutcome
import com.example.testbuild01.data.repository.TicketDraft
import com.example.testbuild01.data.repository.TicketRepository
import com.example.testbuild01.ui.hotel.HotelLinkDialog
import com.example.testbuild01.ui.hotel.TripsState
import com.example.testbuild01.ui.hotel.toUserMessage
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

/**
 * 스캔 → 수동 입력 → 목록 화면이 같은 인스턴스를 공유한다(Activity 범위).
 * 앱에서는 [Factory]로 만들고, UI 테스트에서는 가짜 서버를 넣은 [HotelScheduleLinker]로 직접 만든다.
 */
class TicketViewModel(
    private val repository: TicketRepository,
    private val linker: HotelScheduleLinker
) : ViewModel() {

    val tickets: StateFlow<List<Ticket>> = repository.observeAll()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    /** 스캔으로 얻은 바코드. 입력 화면에서 프리필에 사용한다. */
    var scanResult by mutableStateOf<ScanOutcome.Success?>(null)
        private set

    /** 수정 중인 티켓. null이면 신규 등록. */
    var editingTicket by mutableStateOf<Ticket?>(null)
        private set

    /** 호텔 확인서 OCR로 얻은 후보값. 확인/수정 화면 프리필에 사용한다. */
    var hotelOcrCandidate by mutableStateOf<HotelOcrCandidate?>(null)
        private set

    /** 호텔 ↔ 여행 일정 연동 다이얼로그. 저장 후 목록 화면으로 돌아가도 이어서 보이도록 여기서 들고 있는다. */
    var linkDialog by mutableStateOf<HotelLinkDialog?>(null)
        private set

    /** 서버 호출 중에는 버튼을 막아 같은 요청(특히 일정 생성)이 두 번 나가지 않게 한다. */
    var linkBusy by mutableStateOf(false)
        private set

    /** 한 번 보여주고 [consumeLinkMessage]로 지우는 안내 문구 */
    var linkMessage by mutableStateOf<String?>(null)
        private set

    fun startNew() {
        scanResult = null
        editingTicket = null
        hotelOcrCandidate = null
    }

    fun startEdit(ticket: Ticket) {
        scanResult = null
        editingTicket = ticket
        hotelOcrCandidate = null
    }

    fun onScanned(result: ScanOutcome.Success) {
        scanResult = result
    }

    fun onHotelScanned(candidate: HotelOcrCandidate) {
        hotelOcrCandidate = candidate
    }

    fun save(draft: TicketDraft, onSaved: () -> Unit) {
        val editing = editingTicket
        viewModelScope.launch {
            // 서버와 무관하게 로컬 저장을 먼저 끝낸다. 일정 반영/갱신은 그 뒤에 사용자에게 따로 묻는다.
            if (editing != null && draft.type == TicketType.HOTEL) {
                // 로컬 저장 + "일정도 수정할지" 판단은 linker가 한다(단위 테스트와 같은 경로).
                if (linker.updateHotel(editing.id, draft)) {
                    linkDialog = HotelLinkDialog.ConfirmSyncAfterEdit(editing.id)
                }
            } else if (editing != null) {
                repository.update(editing.id, draft)
            } else {
                val id = repository.add(draft)
                if (draft.type == TicketType.HOTEL && draft.hotel != null) openTripPicker(id, askFirst = true)
            }
            startNew()
            onSaved()
        }
    }

    // ---- 일정에 반영 ----

    /** 등록 직후(askFirst=true, "반영할까요?"부터) 또는 카드의 "일정에 반영"(바로 여행 선택)에서 호출한다. */
    fun openTripPicker(ticketId: Long, askFirst: Boolean) {
        viewModelScope.launch {
            val ticket = repository.getById(ticketId) ?: return@launch
            if (ticket.isLinkedToSchedule) {
                showManageLink(ticket)
                return@launch
            }
            if (ticket.hotel == null) {
                // 키셋 유실 등으로 상세 정보를 복호화하지 못하면 숙박 기간을 알 수 없어 반영할 수 없다.
                linkDialog = null
                linkMessage = "호텔 예약 정보를 읽을 수 없어 일정에 반영할 수 없습니다. '수정'에서 호텔 정보를 다시 입력해 주세요."
                return@launch
            }
            linkDialog = HotelLinkDialog.PickTrip(ticketId, askFirst, TripsState.Loading)
            loadTripsInto(ticketId)
        }
    }

    fun proceedToTripList() {
        val dialog = linkDialog as? HotelLinkDialog.PickTrip ?: return
        linkDialog = dialog.copy(askFirst = false)
    }

    fun retryLoadTrips() {
        val dialog = linkDialog as? HotelLinkDialog.PickTrip ?: return
        linkDialog = dialog.copy(trips = TripsState.Loading)
        viewModelScope.launch { loadTripsInto(dialog.ticketId) }
    }

    private suspend fun loadTripsInto(ticketId: Long) {
        val trips = when (val result = linker.loadTripChoices(ticketId)) {
            is RemoteResult.Success -> TripsState.Loaded(result.value)
            is RemoteResult.Failure -> TripsState.Error(result.error)
        }
        // 불러오는 사이 사용자가 닫았거나 다른 다이얼로그로 바뀌었으면 덮어쓰지 않는다.
        val current = linkDialog as? HotelLinkDialog.PickTrip ?: return
        if (current.ticketId == ticketId) linkDialog = current.copy(trips = trips)
    }

    fun chooseTrip(tripId: Int) {
        val dialog = linkDialog as? HotelLinkDialog.PickTrip ?: return
        runLinkAction {
            when (val outcome = linker.link(dialog.ticketId, tripId)) {
                LinkOutcome.Success -> {
                    linkDialog = null
                    linkMessage = "여행 일정에 호텔 체크인·체크아웃을 추가했습니다."
                }
                is LinkOutcome.AlreadyLinked -> repository.getById(dialog.ticketId)?.let { showManageLink(it) }
                is LinkOutcome.Failed -> linkMessage = outcome.error.toUserMessage()
                LinkOutcome.NotAvailable, LinkOutcome.NotLinked -> {
                    linkDialog = null
                    linkMessage = "호텔 정보를 읽을 수 없어 일정에 반영하지 못했습니다."
                }
            }
        }
    }

    // ---- 이미 연결된 티켓 관리 ----

    private fun showManageLink(ticket: Ticket) {
        linkDialog = HotelLinkDialog.ManageLink(ticket, tripTitle = null)
        viewModelScope.launch {
            // 여행 제목은 보여주기용이라 실패해도 ID로 대신 표시한다.
            val trips = (linker.loadTrips() as? RemoteResult.Success)?.value ?: return@launch
            val current = linkDialog as? HotelLinkDialog.ManageLink ?: return@launch
            if (current.ticket.id != ticket.id) return@launch
            linkDialog = current.copy(tripTitle = trips.firstOrNull { it.id == ticket.linkedTripId }?.title)
        }
    }

    fun unlink(deleteSchedule: Boolean) {
        val ticketId = linkedTicketIdOfDialog() ?: return
        runLinkAction {
            when (val outcome = linker.unlink(ticketId, deleteSchedule)) {
                LinkOutcome.Success, LinkOutcome.NotLinked -> {
                    linkDialog = null
                    linkMessage = if (deleteSchedule) "연결된 일정을 삭제하고 연결을 해제했습니다."
                    else "연결을 해제했습니다. 여행 일정은 그대로 남아 있어요."
                }
                is LinkOutcome.Failed -> linkMessage = outcome.error.toUserMessage() + " 연결은 그대로 유지됩니다."
                else -> linkDialog = null
            }
        }
    }

    /** "로컬만 수정"을 골랐던 티켓을 나중에 현재 호텔 정보로 다시 맞출 때 쓴다. */
    fun syncNow() {
        val ticketId = linkedTicketIdOfDialog() ?: return
        runLinkAction { handleSyncOutcome(ticketId, linker.syncLinkedSchedule(ticketId)) }
    }

    private fun linkedTicketIdOfDialog(): Long? = when (val dialog = linkDialog) {
        is HotelLinkDialog.ManageLink -> dialog.ticket.id
        is HotelLinkDialog.SyncFailed -> dialog.ticketId
        else -> null
    }

    // ---- 수정 후 일정 갱신 ----

    fun respondSyncAfterEdit(accept: Boolean) {
        val dialog = linkDialog as? HotelLinkDialog.ConfirmSyncAfterEdit ?: return
        if (!accept) {
            linkDialog = null
            linkMessage = "티켓만 수정했습니다. 여행 일정의 시간·장소와 달라질 수 있어요. 카드의 '일정 연결'에서 언제든 다시 맞출 수 있습니다."
            return
        }
        runLinkAction { handleSyncOutcome(dialog.ticketId, linker.syncLinkedSchedule(dialog.ticketId)) }
    }

    private fun handleSyncOutcome(ticketId: Long, outcome: LinkOutcome) {
        when (outcome) {
            LinkOutcome.Success -> {
                linkDialog = null
                linkMessage = "연결된 여행 일정도 수정했습니다."
            }
            is LinkOutcome.Failed -> {
                val kind = outcome.error.kind
                // 일정이 사라졌거나 여행에서 빠진 경우엔 재시도로 해결되지 않으므로 연결 해제를 제안한다.
                linkDialog = if (kind == SyncErrorKind.NOT_FOUND || kind == SyncErrorKind.FORBIDDEN) {
                    HotelLinkDialog.SyncFailed(ticketId, outcome.error)
                } else {
                    null
                }
                linkMessage = outcome.error.toUserMessage() + " 티켓 수정 내용은 기기에 저장되어 있어요."
            }
            else -> linkDialog = null
        }
    }

    // ---- 삭제 ----

    fun requestDelete(ticket: Ticket) {
        linkDialog = HotelLinkDialog.ConfirmDelete(ticket)
    }

    fun confirmDelete(deleteSchedule: Boolean) {
        val dialog = linkDialog as? HotelLinkDialog.ConfirmDelete ?: return
        runLinkAction {
            when (val outcome = linker.deleteTicket(dialog.ticket, deleteSchedule)) {
                is LinkOutcome.Failed -> {
                    // 티켓은 지우지 않았다. 같은 다이얼로그에 오류를 보여 "티켓만 삭제"를 다시 고를 수 있게 한다.
                    linkDialog = dialog.copy(error = outcome.error)
                }
                else -> linkDialog = null
            }
        }
    }

    fun dismissLinkDialog() {
        if (!linkBusy) linkDialog = null
    }

    fun consumeLinkMessage() {
        linkMessage = null
    }

    private fun runLinkAction(block: suspend () -> Unit) {
        if (linkBusy) return
        linkBusy = true
        viewModelScope.launch {
            try {
                block()
            } finally {
                linkBusy = false
            }
        }
    }

    companion object {
        val Factory = viewModelFactory {
            initializer {
                val app = this[APPLICATION_KEY] as MainApplication
                TicketViewModel(app.ticketRepository, app.hotelScheduleLinker)
            }
        }
    }
}
