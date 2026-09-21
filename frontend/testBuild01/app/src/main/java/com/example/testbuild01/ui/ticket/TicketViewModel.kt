package com.example.testbuild01.ui.ticket

import android.app.Application
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.example.testbuild01.MainApplication
import com.example.testbuild01.data.model.Ticket
import com.example.testbuild01.data.repository.TicketDraft
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

/** 스캔 → 수동 입력 → 목록 화면이 같은 인스턴스를 공유한다(Activity 범위). */
class TicketViewModel(application: Application) : AndroidViewModel(application) {

    private val repository = (application as MainApplication).ticketRepository

    val tickets: StateFlow<List<Ticket>> = repository.observeAll()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    /** 스캔으로 얻은 바코드. 입력 화면에서 프리필에 사용한다. */
    var scanResult by mutableStateOf<ScanOutcome.Success?>(null)
        private set

    /** 수정 중인 티켓. null이면 신규 등록. */
    var editingTicket by mutableStateOf<Ticket?>(null)
        private set

    fun startNew() {
        scanResult = null
        editingTicket = null
    }

    fun startEdit(ticket: Ticket) {
        scanResult = null
        editingTicket = ticket
    }

    fun onScanned(result: ScanOutcome.Success) {
        scanResult = result
    }

    fun save(draft: TicketDraft, onSaved: () -> Unit) {
        val editing = editingTicket
        viewModelScope.launch {
            if (editing != null) repository.update(editing.id, draft) else repository.add(draft)
            startNew()
            onSaved()
        }
    }

    fun delete(ticket: Ticket) {
        viewModelScope.launch { repository.delete(ticket) }
    }
}
