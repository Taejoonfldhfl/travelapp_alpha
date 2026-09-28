package com.example.testbuild01.data.model

// 서버 ExpenseCategory enum 과 이름이 같아야 한다 (문자열로 직렬화됨).
enum class ExpenseCategory(val label: String) {
    FOOD("식비"),
    TRANSPORT("교통"),
    LODGING("숙박"),
    SHOPPING("쇼핑"),
    ETC("기타")
}

data class ExpenseSplitDto(
    val userId: Int,
    val shareAmount: Double
)

data class ExpenseUpsertRequest(
    val paidByUserId: Int,
    val amount: Double,
    val category: ExpenseCategory,
    val date: String,               // ISO-8601 (yyyy-MM-dd'T'HH:mm:ss)
    val merchantName: String,
    val memo: String = "",
    val scheduleId: Int? = null,
    val splitMemberIds: List<Int> = emptyList(), // splits 가 비어 있을 때 균등분할 대상
    val splits: List<ExpenseSplitDto> = emptyList()
)

data class ExpenseResponse(
    val id: Int,
    val tripId: Int,
    val paidByUserId: Int,
    val amount: Double,
    val category: ExpenseCategory,
    val date: String,
    val merchantName: String,
    val memo: String,
    val scheduleId: Int?,
    val splits: List<ExpenseSplitDto>
)

data class BudgetSummary(
    val budget: Double?,
    val totalSpent: Double,
    val remaining: Double?,
    val isOverBudget: Boolean
)

data class BudgetUpdateRequest(val budgetAmount: Double?)

data class ExpenseBreakdownItem(
    val category: ExpenseCategory,
    val total: Double,
    val ratio: Double
)

data class SettlementTransfer(
    val fromUserId: Int,
    val toUserId: Int,
    val amount: Double
)

// 확정(finalize)된 정산 스냅샷. 확정 이후 지출이 바뀌어도 transfers는 확정 시점 값 그대로다.
data class SettlementResult(
    val id: Int,
    val tripId: Int,
    val finalizedByUserId: Int,
    val finalizedAt: String,
    val transfers: List<SettlementTransfer>
)
