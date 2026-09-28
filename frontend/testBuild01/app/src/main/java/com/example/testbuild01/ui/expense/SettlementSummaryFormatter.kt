package com.example.testbuild01.ui.expense

import com.example.testbuild01.data.model.SettlementTransfer

// 정산 결과를 공유용 텍스트로 포맷한다. 순수 함수라서 단위 테스트할 수 있다.
// 예: "김태준 → 이OO : 15,000원" 형태로 한 줄씩.
internal fun formatSettlementSummary(
    tripTitle: String,
    transfers: List<SettlementTransfer>,
    nameOf: (Int) -> String
): String {
    if (transfers.isEmpty()) return "[$tripTitle] 정산할 내역이 없어요."
    return buildString {
        appendLine("[$tripTitle] 정산 결과")
        transfers.forEach { appendLine("${nameOf(it.fromUserId)} → ${nameOf(it.toUserId)} : ${won(it.amount)}") }
    }.trimEnd()
}
