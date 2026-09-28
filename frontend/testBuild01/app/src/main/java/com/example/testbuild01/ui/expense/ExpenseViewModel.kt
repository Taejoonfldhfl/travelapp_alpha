package com.example.testbuild01.ui.expense

import android.app.Application
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.example.testbuild01.data.local.TokenManager
import com.example.testbuild01.data.model.*
import com.example.testbuild01.data.network.RetrofitClient
import com.example.testbuild01.notification.ExpenseNotifier
import kotlinx.coroutines.launch
import retrofit2.Response

// 가계부 전 화면이 공유하는 ViewModel. 데이터의 source of truth 는 서버(PostgreSQL)이며
// 여기서는 화면 표시용 스냅샷만 들고 있다.
class ExpenseViewModel(app: Application) : AndroidViewModel(app) {
    private val api get() = RetrofitClient.instance

    val currentUserId: Int? = TokenManager(app).getUserId()

    var tripId by mutableStateOf(0)
        private set
    var expenses by mutableStateOf<List<ExpenseResponse>>(emptyList())
        private set
    var members by mutableStateOf<List<TripMemberResponse>>(emptyList())
        private set
    var schedules by mutableStateOf<List<ScheduleResponse>>(emptyList())
        private set
    var summary by mutableStateOf<BudgetSummary?>(null)
        private set
    var breakdown by mutableStateOf<List<ExpenseBreakdownItem>>(emptyList())
        private set
    var settlement by mutableStateOf<List<SettlementTransfer>>(emptyList())
        private set
    var isLoading by mutableStateOf(false)
        private set
    var errorMessage by mutableStateOf<String?>(null)

    // 영수증 스캔이 인식한 금액. 저장 전에 반드시 확인/수정 화면을 거친다.
    var scannedAmount by mutableStateOf<Long?>(null)
        private set

    fun updateScannedAmount(amount: Long?) {
        scannedAmount = amount
    }

    fun memberName(userId: Int): String {
        val m = members.firstOrNull { it.userId == userId }
        return m?.nickname?.takeIf { it.isNotBlank() } ?: m?.email ?: "멤버 $userId"
    }

    fun load(tripId: Int) {
        this.tripId = tripId
        viewModelScope.launch {
            isLoading = true
            try {
                api.getTripMembersSuspend(tripId).body()?.let { members = it }
                api.getSchedulesSuspend(tripId).body()?.let { schedules = it }
                refreshAll()
            } catch (e: Exception) {
                errorMessage = "불러오기 실패: ${e.message}"
            } finally {
                isLoading = false
            }
        }
    }

    private suspend fun refreshAll() {
        api.getExpenses(tripId).body()?.let { expenses = it }
        api.getBudgetSummary(tripId).body()?.let { summary = it }
        api.getExpenseBreakdown(tripId).body()?.let { breakdown = it }
        api.getSettlement(tripId).body()?.let { settlement = it }
    }

    fun refresh() {
        viewModelScope.launch {
            try {
                refreshAll()
            } catch (e: Exception) {
                errorMessage = "새로고침 실패: ${e.message}"
            }
        }
    }

    // expenseId 가 null 이면 등록, 값이 있으면 수정. 성공하면 예산을 재조회해서 초과 시 즉시 알림을 띄운다.
    fun saveExpense(expenseId: Int?, request: ExpenseUpsertRequest, onDone: (Boolean) -> Unit) {
        viewModelScope.launch {
            try {
                val res: Response<ExpenseResponse> =
                    if (expenseId == null) api.createExpense(tripId, request)
                    else api.updateExpense(expenseId, request)

                if (!res.isSuccessful) {
                    errorMessage = res.errorBody()?.string()?.takeIf { it.isNotBlank() }
                        ?: "저장 실패 (${res.code()})"
                    onDone(false)
                    return@launch
                }

                scannedAmount = null
                refreshAll()
                notifyIfOverBudget()
                onDone(true)
            } catch (e: Exception) {
                errorMessage = "저장 실패: ${e.message}"
                onDone(false)
            }
        }
    }

    // 정산 확정: 서버에 현재 정산 결과를 스냅샷으로 저장하고, 같은 여행 멤버(본인 제외)에게 알림을 보낸다.
    fun finalizeSettlement(onDone: (Boolean) -> Unit) {
        viewModelScope.launch {
            try {
                val res = api.finalizeSettlement(tripId)
                if (res.isSuccessful) {
                    onDone(true)
                } else {
                    errorMessage = "정산 확정 실패 (${res.code()})"
                    onDone(false)
                }
            } catch (e: Exception) {
                errorMessage = "정산 확정 실패: ${e.message}"
                onDone(false)
            }
        }
    }

    fun deleteExpense(expenseId: Int) {
        viewModelScope.launch {
            try {
                if (api.deleteExpense(expenseId).isSuccessful) refreshAll()
            } catch (e: Exception) {
                errorMessage = "삭제 실패: ${e.message}"
            }
        }
    }

    fun updateBudget(amount: Double?) {
        viewModelScope.launch {
            try {
                if (api.setBudget(tripId, BudgetUpdateRequest(amount)).isSuccessful) {
                    refreshAll()
                    notifyIfOverBudget()
                } else {
                    errorMessage = "예산은 여행 소유자만 설정할 수 있습니다."
                }
            } catch (e: Exception) {
                errorMessage = "예산 설정 실패: ${e.message}"
            }
        }
    }

    // 방금 갱신된 budget-summary 가 초과 상태면 로컬 알림을 발송한다.
    private fun notifyIfOverBudget() {
        val s = summary ?: return
        val remaining = s.remaining ?: return
        if (s.isOverBudget) {
            ExpenseNotifier.showBudgetExceeded(getApplication(), tripId, -remaining)
        }
    }
}
