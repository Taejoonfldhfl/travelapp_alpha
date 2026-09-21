@file:OptIn(ExperimentalMaterial3Api::class)

package com.example.testbuild01.ui.expense

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.model.BudgetSummary
import com.example.testbuild01.data.model.ExpenseResponse

internal fun won(amount: Double): String = "${"%,d".format(amount.toLong())}원"

@Composable
fun ExpenseListScreen(
    viewModel: ExpenseViewModel,
    tripId: Int,
    onBack: () -> Unit,
    onScan: () -> Unit,
    onAdd: () -> Unit,
    onEdit: (Int) -> Unit,
    onStats: () -> Unit,
    onSettlement: () -> Unit
) {
    LaunchedEffect(tripId) { viewModel.load(tripId) }
    var showBudgetDialog by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("가계부") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "뒤로 가기")
                    }
                }
            )
        },
        floatingActionButton = {
            Column(horizontalAlignment = Alignment.End, verticalArrangement = Arrangement.spacedBy(8.dp)) {
                SmallFloatingActionButton(onClick = onAdd) { Icon(Icons.Default.Add, "직접 입력") }
                ExtendedFloatingActionButton(onClick = onScan) { Text("영수증 스캔") }
            }
        }
    ) { padding ->
        Column(modifier = Modifier.padding(padding).padding(horizontal = 16.dp)) {
            BudgetSummaryCard(viewModel.summary, onEditBudget = { showBudgetDialog = true })

            Row(
                modifier = Modifier.fillMaxWidth().padding(vertical = 8.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                OutlinedButton(onClick = onStats, modifier = Modifier.weight(1f)) { Text("통계") }
                OutlinedButton(onClick = onSettlement, modifier = Modifier.weight(1f)) { Text("정산") }
            }

            if (viewModel.expenses.isEmpty() && !viewModel.isLoading) {
                Text("등록된 지출이 없습니다.", modifier = Modifier.padding(16.dp))
            }

            LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                items(viewModel.expenses, key = { it.id }) { item ->
                    ExpenseRow(item, viewModel.memberName(item.paidByUserId), onEdit = { onEdit(item.id) },
                        onDelete = { viewModel.deleteExpense(item.id) })
                }
                item { Spacer(Modifier.height(120.dp)) }
            }
        }
    }

    if (showBudgetDialog) {
        BudgetDialog(
            current = viewModel.summary?.budget,
            onDismiss = { showBudgetDialog = false },
            onConfirm = {
                viewModel.updateBudget(it)
                showBudgetDialog = false
            }
        )
    }
}

// 예산 대비 잔액. 초과 시 경고색으로 표시한다.
@Composable
fun BudgetSummaryCard(summary: BudgetSummary?, onEditBudget: () -> Unit) {
    val over = summary?.isOverBudget == true
    val container = if (over) MaterialTheme.colorScheme.errorContainer else MaterialTheme.colorScheme.secondaryContainer
    val content = if (over) MaterialTheme.colorScheme.onErrorContainer else MaterialTheme.colorScheme.onSecondaryContainer

    Card(
        colors = CardDefaults.cardColors(containerColor = container, contentColor = content),
        modifier = Modifier.fillMaxWidth().clickable(onClick = onEditBudget)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            if (summary?.budget == null) {
                Text("예산이 설정되지 않았어요", style = MaterialTheme.typography.titleMedium)
                Text("탭해서 예산을 설정하세요 (여행 소유자만 가능)", style = MaterialTheme.typography.bodySmall)
                summary?.let { Text("총 지출 ${won(it.totalSpent)}", modifier = Modifier.padding(top = 4.dp)) }
            } else {
                Text("예산 ${won(summary.budget)}  ·  지출 ${won(summary.totalSpent)}")
                val remaining = summary.remaining ?: 0.0
                Text(
                    if (over) "예산 초과 ${won(-remaining)}" else "남은 예산 ${won(remaining)}",
                    style = MaterialTheme.typography.headlineSmall
                )
                LinearProgressIndicator(
                    progress = { (summary.totalSpent / summary.budget).toFloat().coerceIn(0f, 1f) },
                    color = if (over) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.primary,
                    modifier = Modifier.fillMaxWidth().padding(top = 8.dp)
                )
            }
        }
    }
}

@Composable
private fun ExpenseRow(item: ExpenseResponse, payerName: String, onEdit: () -> Unit, onDelete: () -> Unit) {
    Card(modifier = Modifier.fillMaxWidth().clickable(onClick = onEdit)) {
        Row(
            modifier = Modifier.padding(16.dp).fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(item.merchantName, style = MaterialTheme.typography.titleMedium)
                Text(
                    "${item.date.take(10)} · ${item.category.label} · $payerName 결제",
                    style = MaterialTheme.typography.bodySmall
                )
            }
            Text(won(item.amount), style = MaterialTheme.typography.titleMedium)
            IconButton(onClick = onDelete) { Icon(Icons.Default.Delete, "삭제") }
        }
    }
}

@Composable
private fun BudgetDialog(current: Double?, onDismiss: () -> Unit, onConfirm: (Double?) -> Unit) {
    var text by remember { mutableStateOf(current?.toLong()?.toString() ?: "") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("여행 예산 설정") },
        text = {
            OutlinedTextField(
                value = text,
                onValueChange = { text = it.filter(Char::isDigit) },
                label = { Text("예산 (원), 비우면 해제") },
                keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(keyboardType = KeyboardType.Number),
                singleLine = true
            )
        },
        confirmButton = { TextButton(onClick = { onConfirm(text.toLongOrNull()?.toDouble()) }) { Text("저장") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("취소") } }
    )
}
