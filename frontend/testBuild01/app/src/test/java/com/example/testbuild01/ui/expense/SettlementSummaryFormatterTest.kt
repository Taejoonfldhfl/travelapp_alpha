package com.example.testbuild01.ui.expense

import com.example.testbuild01.data.model.SettlementTransfer
import org.junit.Assert.assertEquals
import org.junit.Test

class SettlementSummaryFormatterTest {
    private val names = mapOf(1 to "김태준", 2 to "이OO", 3 to "박OO")

    @Test
    fun twoPeople_singleTransfer() {
        val text = formatSettlementSummary(
            "제주 여행",
            listOf(SettlementTransfer(fromUserId = 2, toUserId = 1, amount = 15000.0)),
            { names.getValue(it) }
        )
        assertEquals("[제주 여행] 정산 결과\n이OO → 김태준 : 15,000원", text)
    }

    @Test
    fun threeOrMorePeople_listsOneLinePerTransfer() {
        val transfers = listOf(
            SettlementTransfer(fromUserId = 2, toUserId = 1, amount = 15000.0),
            SettlementTransfer(fromUserId = 3, toUserId = 1, amount = 8000.0)
        )

        val text = formatSettlementSummary("부산 여행", transfers, { names.getValue(it) })

        assertEquals(
            "[부산 여행] 정산 결과\n이OO → 김태준 : 15,000원\n박OO → 김태준 : 8,000원",
            text
        )
    }

    @Test
    fun zeroAmountTransfer_stillRendersAsZeroWon() {
        val text = formatSettlementSummary(
            "당일치기",
            listOf(SettlementTransfer(fromUserId = 2, toUserId = 1, amount = 0.0)),
            { names.getValue(it) }
        )
        assertEquals("[당일치기] 정산 결과\n이OO → 김태준 : 0원", text)
    }

    @Test
    fun noTransfers_showsFriendlyEmptyMessage() {
        val text = formatSettlementSummary("당일치기", emptyList()) { names.getValue(it) }
        assertEquals("[당일치기] 정산할 내역이 없어요.", text)
    }
}
