@file:OptIn(ExperimentalMaterial3Api::class)

package com.example.testbuild01.ui.expense

import android.content.Intent
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.model.SettlementTransfer

// 정산 요약 텍스트. 순수 함수라서 단위 테스트할 수 있다.
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

@Composable
fun SettlementScreen(viewModel: ExpenseViewModel, tripTitle: String, onBack: () -> Unit) {
    val context = LocalContext.current
    LaunchedEffect(Unit) { viewModel.refresh() }
    val transfers = viewModel.settlement

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("정산 결과") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "뒤로 가기")
                    }
                }
            )
        }
    ) { padding ->
        Column(modifier = Modifier.padding(padding).padding(16.dp)) {
            if (transfers.isEmpty()) {
                Text("정산할 내역이 없어요.")
            }

            LazyColumn(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                items(transfers) { t ->
                    Card(modifier = Modifier.fillMaxWidth()) {
                        Row(
                            modifier = Modifier.padding(16.dp).fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Text("${viewModel.memberName(t.fromUserId)} → ${viewModel.memberName(t.toUserId)}")
                            Text(won(t.amount), style = MaterialTheme.typography.titleMedium)
                        }
                    }
                }
            }

            Button(
                onClick = {
                    val text = formatSettlementSummary(tripTitle, transfers, viewModel::memberName)
                    val send = Intent(Intent.ACTION_SEND).apply {
                        type = "text/plain"
                        putExtra(Intent.EXTRA_TEXT, text)
                    }
                    context.startActivity(Intent.createChooser(send, "정산 요약 공유"))
                },
                enabled = transfers.isNotEmpty(),
                modifier = Modifier.fillMaxWidth()
            ) { Text("정산 요약 공유") }
        }
    }
}
