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
                    // 확정 API 호출(같은 여행 멤버에게 알림)과 외부 공유 시트는 동시에 진행한다.
                    // 확정이 실패해도(네트워크 오류 등) 공유 자체는 막지 않는다 — 실패는 errorMessage로 별도 노출.
                    viewModel.finalizeSettlement { }

                    val text = formatSettlementSummary(tripTitle, transfers, viewModel::memberName)
                    val send = Intent(Intent.ACTION_SEND).apply {
                        type = "text/plain"
                        putExtra(Intent.EXTRA_TEXT, text)
                    }
                    context.startActivity(Intent.createChooser(send, "정산 요약 공유"))
                },
                enabled = transfers.isNotEmpty(),
                modifier = Modifier.fillMaxWidth()
            ) { Text("정산 확정 및 공유") }
        }
    }
}
