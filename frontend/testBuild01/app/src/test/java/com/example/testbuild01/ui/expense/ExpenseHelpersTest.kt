package com.example.testbuild01.ui.expense

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

    @Test
    fun extractPrice_picksLargestAmount() {
        assertEquals(45000L, extractPrice("아메리카노 4,500\n합계 45,000\n전화 010"))
    }

    @Test
    fun extractPrice_noAmountReturnsNull() {
        assertNull(extractPrice("감사합니다 12"))
    }
}
