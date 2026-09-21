package com.example.testbuild01.ui.expense

import com.example.testbuild01.data.model.ExpenseCategory
import com.example.testbuild01.data.model.ExpenseResponse
import com.example.testbuild01.data.model.ExpenseSplitDto
import com.example.testbuild01.data.model.SettlementTransfer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class ExpenseHelpersTest {
    private val names = mapOf(1 to "민수", 2 to "지영")

    @Test
    fun settlementSummary_listsWhoPaysWhom() {
        val text = formatSettlementSummary(
            "제주 여행",
            listOf(SettlementTransfer(fromUserId = 2, toUserId = 1, amount = 12500.0)),
            { names.getValue(it) }
        )
        assertEquals("[제주 여행] 정산 결과\n지영 → 민수 : 12,500원", text)
    }

    @Test
    fun settlementSummary_emptyIsFriendlyMessage() {
        assertEquals("[제주 여행] 정산할 내역이 없어요.", formatSettlementSummary("제주 여행", emptyList()) { "" })
    }

    private fun expense(id: Int, date: String, amount: Double, vararg splits: Pair<Int, Double>) = ExpenseResponse(
        id = id, tripId = 1, paidByUserId = 1, amount = amount, category = ExpenseCategory.FOOD,
        date = date, merchantName = "m$id", memo = "", scheduleId = null,
        splits = splits.map { ExpenseSplitDto(it.first, it.second) }
    )

    @Test
    fun myShares_keepsOnlyMyShareAmount_newestFirst() {
        val expenses = listOf(
            expense(1, "2026-10-01T00:00:00Z", 30000.0, 1 to 10000.0, 2 to 20000.0),
            expense(2, "2026-10-02T00:00:00Z", 5000.0, 2 to 5000.0),            // 내 분담 없음 → 제외
            expense(3, "2026-10-03T00:00:00Z", 8000.0, 1 to 4000.0, 2 to 4000.0)
        )

        val mine = computeMyShares(expenses, userId = 1)

        assertEquals(listOf(3, 1), mine.map { it.expenseId })
        assertEquals(listOf(4000.0, 10000.0), mine.map { it.myShare })
        assertEquals(14000.0, mine.sumOf { it.myShare }, 0.0)
    }

    @Test
    fun myShares_unknownUserIsEmpty() {
        assertEquals(emptyList<MyShareItem>(), computeMyShares(listOf(expense(1, "2026-10-01T00:00:00Z", 1000.0, 1 to 1000.0)), null))
    }

    @Test
    fun extractPrice_picksLargestAmount() {
        assertEquals(45000L, extractPrice("아메리카노 4,500\n합계 45,000\n전화 010"))
    }

    @Test
    fun extractPrice_noAmountReturnsNull() {
        assertNull(extractPrice("감사합니다 12"))
    }
}
