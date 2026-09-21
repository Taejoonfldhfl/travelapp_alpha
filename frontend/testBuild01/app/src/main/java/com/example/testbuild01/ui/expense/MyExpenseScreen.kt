@file:OptIn(ExperimentalMaterial3Api::class)

package com.example.testbuild01.ui.expense

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.model.ExpenseCategory
import com.example.testbuild01.data.model.ExpenseResponse

// 로그인한 사용자 본인이 부담하는 지출 한 건.
data class MyShareItem(
    val expenseId: Int,
    val merchantName: String,
    val date: String,
    val category: ExpenseCategory,
    val totalAmount: Double,
    val myShare: Double
)

// 각 지출의 분담(ExpenseSplit) 중 내 것만 골라낸다. 내 분담이 없는 지출은 제외하고 최신 날짜순으로 정렬한다.
internal fun computeMyShares(expenses: List<ExpenseResponse>, userId: Int?): List<MyShareItem> {
    if (userId == null) return emptyList()
    return expenses.mapNotNull { e ->
        val mine = e.splits.firstOrNull { it.userId == userId } ?: return@mapNotNull null
        MyShareItem(e.id, e.merchantName, e.date, e.category, e.amount, mine.shareAmount)
    }.sortedWith(compareByDescending<MyShareItem> { it.date }.thenByDescending { it.expenseId })
}

@Composable
fun MyExpenseScreen(viewModel: ExpenseViewModel, onBack: () -> Unit) {
    LaunchedEffect(Unit) { viewModel.refresh() }
    val items = remember(viewModel.expenses, viewModel.currentUserId) {
        computeMyShares(viewModel.expenses, viewModel.currentUserId)
    }
    val total = items.sumOf { it.myShare }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("내 지출 요약") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "뒤로 가기")
                    }
                }
            )
        }
    ) { padding ->
        Column(modifier = Modifier.padding(padding).padding(16.dp)) {
            Card(
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text("내가 부담할 총 금액", style = MaterialTheme.typography.bodyMedium)
                    Text(won(total), style = MaterialTheme.typography.headlineSmall)
                    Text("${items.size}건", style = MaterialTheme.typography.bodySmall)
                }
            }

            Spacer(Modifier.height(12.dp))

            if (items.isEmpty()) {
                Text(
                    if (viewModel.currentUserId == null) "로그인 정보를 확인할 수 없어요." else "내가 부담하는 지출이 없어요."
                )
            }

            LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                items(items, key = { it.expenseId }) { item ->
                    Card(modifier = Modifier.fillMaxWidth()) {
                        Row(
                            modifier = Modifier.padding(16.dp).fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text(item.merchantName, style = MaterialTheme.typography.titleMedium)
                                Text(
                                    "${item.date.take(10)} · ${item.category.label} · 전체 ${won(item.totalAmount)}",
                                    style = MaterialTheme.typography.bodySmall
                                )
                            }
                            Text(won(item.myShare), style = MaterialTheme.typography.titleMedium)
                        }
                    }
                }
            }
        }
    }
}
