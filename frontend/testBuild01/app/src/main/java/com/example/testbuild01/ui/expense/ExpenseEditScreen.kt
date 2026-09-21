@file:OptIn(ExperimentalMaterial3Api::class)

package com.example.testbuild01.ui.expense

import android.Manifest
import android.os.Build
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.model.*
import java.time.LocalDate

// 영수증 인식 결과(또는 수동 입력)를 저장하기 전에 확인/수정하는 화면. expenseId 가 있으면 수정 모드.
@Composable
fun ExpenseEditScreen(
    viewModel: ExpenseViewModel,
    expenseId: Int?,
    onBack: () -> Unit
) {
    val context = LocalContext.current
    val existing = remember(expenseId, viewModel.expenses) { viewModel.expenses.firstOrNull { it.id == expenseId } }
    val members = viewModel.members

    // 저장 후 예산 초과 알림을 띄울 수 있도록 알림 권한을 미리 요청한다 (API 33+).
    val notificationLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { }
    LaunchedEffect(Unit) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            notificationLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }

    var amountText by remember {
        mutableStateOf(
            existing?.amount?.toLong()?.toString() ?: viewModel.scannedAmount?.toString() ?: ""
        )
    }
    var category by remember { mutableStateOf(existing?.category ?: ExpenseCategory.ETC) }
    var scheduleId by remember { mutableStateOf(existing?.scheduleId) }
    // 가맹점명·날짜는 하드코딩 없이 사용자가 직접 입력한다. 날짜만 오늘 날짜를 초기 제안값으로 둔다.
    var merchant by remember { mutableStateOf(existing?.merchantName ?: "") }
    var dateText by remember { mutableStateOf(existing?.date?.take(10) ?: LocalDate.now().toString()) }
    var memo by remember { mutableStateOf(existing?.memo ?: "") }
    var payerId by remember { mutableStateOf(existing?.paidByUserId ?: viewModel.currentUserId) }

    var selectedIds by remember {
        mutableStateOf(existing?.splits?.map { it.userId }?.toSet() ?: members.map { it.userId }.toSet())
    }
    var customMode by remember { mutableStateOf(false) }
    var customAmounts by remember {
        mutableStateOf<Map<Int, String>>(
            existing?.splits?.associate { it.userId to it.shareAmount.toLong().toString() } ?: emptyMap()
        )
    }
    var isSaving by remember { mutableStateOf(false) }

    // 멤버 목록이 늦게 로드되는 경우 기본 선택을 채워준다.
    LaunchedEffect(members) {
        if (selectedIds.isEmpty() && existing == null) selectedIds = members.map { it.userId }.toSet()
        if (payerId == null) payerId = members.firstOrNull()?.userId
    }

    val amount = amountText.toLongOrNull()
    val customSum = selectedIds.sumOf { customAmounts[it]?.toLongOrNull() ?: 0L }
    val customValid = !customMode || (amount != null && customSum == amount)
    val dateValid = runCatching { LocalDate.parse(dateText) }.isSuccess
    val canSave = amount != null && amount > 0 && dateValid && merchant.isNotBlank() &&
            payerId != null && selectedIds.isNotEmpty() && customValid && !isSaving

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(if (expenseId == null) "지출 확인/등록" else "지출 수정") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "뒤로 가기")
                    }
                }
            )
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .padding(padding)
                .padding(16.dp)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            OutlinedTextField(
                value = amountText,
                onValueChange = { amountText = it.filter(Char::isDigit) },
                label = { Text("금액 (원)") },
                keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(keyboardType = KeyboardType.Number),
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )

            DropdownField(
                label = "카테고리",
                options = ExpenseCategory.entries.toList(),
                selected = category,
                optionLabel = { it.label },
                onSelected = { category = it }
            )

            DropdownField(
                label = "관련 일정",
                options = listOf<ScheduleResponse?>(null) + viewModel.schedules,
                selected = viewModel.schedules.firstOrNull { it.id == scheduleId },
                optionLabel = { it?.title ?: "선택 안 함" },
                onSelected = { scheduleId = it?.id }
            )

            OutlinedTextField(
                value = merchant,
                onValueChange = { merchant = it },
                label = { Text("가맹점명") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )

            OutlinedTextField(
                value = dateText,
                onValueChange = { dateText = it },
                label = { Text("날짜 (yyyy-MM-dd)") },
                isError = !dateValid,
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )

            OutlinedTextField(
                value = memo,
                onValueChange = { memo = it },
                label = { Text("메모 (선택)") },
                modifier = Modifier.fillMaxWidth()
            )

            DropdownField(
                label = "결제한 사람",
                options = members,
                selected = members.firstOrNull { it.userId == payerId },
                optionLabel = { viewModel.memberName(it.userId) },
                onSelected = { payerId = it.userId }
            )

            HorizontalDivider()
            Text("분담 멤버", style = MaterialTheme.typography.titleMedium)
            Row(verticalAlignment = Alignment.CenterVertically) {
                Switch(checked = customMode, onCheckedChange = { customMode = it })
                Spacer(Modifier.width(8.dp))
                Text(if (customMode) "금액 직접 지정" else "균등 분할")
            }

            members.forEach { m ->
                val checked = m.userId in selectedIds
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(
                        checked = checked,
                        onCheckedChange = {
                            selectedIds = if (it) selectedIds + m.userId else selectedIds - m.userId
                        }
                    )
                    Text(viewModel.memberName(m.userId), modifier = Modifier.weight(1f))
                    if (customMode && checked) {
                        OutlinedTextField(
                            value = customAmounts[m.userId] ?: "",
                            onValueChange = { v -> customAmounts = customAmounts + (m.userId to v.filter(Char::isDigit)) },
                            singleLine = true,
                            keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(keyboardType = KeyboardType.Number),
                            modifier = Modifier.width(130.dp)
                        )
                    } else if (!customMode && checked && amount != null && selectedIds.isNotEmpty()) {
                        Text("${"%,d".format(amount / selectedIds.size)}원", style = MaterialTheme.typography.bodyMedium)
                    }
                }
            }
            if (customMode) {
                Text(
                    "합계 ${"%,d".format(customSum)}원 / 금액 ${"%,d".format(amount ?: 0)}원",
                    color = if (customValid) MaterialTheme.colorScheme.onSurfaceVariant else MaterialTheme.colorScheme.error
                )
            }

            Button(
                onClick = {
                    isSaving = true
                    val request = ExpenseUpsertRequest(
                        paidByUserId = payerId!!,
                        amount = amount!!.toDouble(),
                        category = category,
                        date = "${dateText}T00:00:00Z",
                        merchantName = merchant.trim(),
                        memo = memo.trim(),
                        scheduleId = scheduleId,
                        splitMemberIds = if (customMode) emptyList() else selectedIds.toList(),
                        splits = if (customMode) selectedIds.map {
                            ExpenseSplitDto(it, (customAmounts[it]?.toLongOrNull() ?: 0L).toDouble())
                        } else emptyList()
                    )
                    viewModel.saveExpense(expenseId, request) { ok ->
                        isSaving = false
                        if (ok) onBack()
                        else Toast.makeText(context, viewModel.errorMessage ?: "저장 실패", Toast.LENGTH_LONG).show()
                    }
                },
                enabled = canSave,
                modifier = Modifier.fillMaxWidth()
            ) { Text("저장") }
        }
    }
}

@Composable
private fun <T> DropdownField(
    label: String,
    options: List<T>,
    selected: T?,
    optionLabel: (T) -> String,
    onSelected: (T) -> Unit
) {
    var expanded by remember { mutableStateOf(false) }
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = selected?.let(optionLabel) ?: (if (options.firstOrNull() == null && options.isNotEmpty()) "선택 안 함" else ""),
            onValueChange = {},
            readOnly = true,
            label = { Text(label) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor(ExposedDropdownMenuAnchorType.PrimaryNotEditable).fillMaxWidth()
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            options.forEach { option ->
                DropdownMenuItem(
                    text = { Text(optionLabel(option)) },
                    onClick = {
                        onSelected(option)
                        expanded = false
                    }
                )
            }
        }
    }
}
