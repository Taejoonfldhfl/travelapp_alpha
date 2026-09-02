package com.example.testbuild01.ui.aichat

import android.widget.Toast
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import coil.compose.AsyncImage

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AiChatScreen(
    tripId: Int,
    onBack: () -> Unit
) {
    val viewModel: AiChatViewModel = viewModel(
        factory = AiChatViewModelFactory(tripId)
    )

    val messages by viewModel.messages.collectAsState()
    val isSending by viewModel.isSending.collectAsState()
    val context = LocalContext.current
    val listState = rememberLazyListState()

    var input by remember { mutableStateOf("") }
    var dialogCard by remember { mutableStateOf<ChatMessage.RecommendationCard?>(null) }

    LaunchedEffect(messages.size) {
        if (messages.isNotEmpty()) {
            listState.animateScrollToItem(messages.size - 1)
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("장소추천 AI") },
                navigationIcon = {
                    TextButton(onClick = onBack) {
                        Text("뒤로")
                    }
                }
            )
        },
        bottomBar = {
            Surface(tonalElevation = 4.dp) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    OutlinedTextField(
                        value = input,
                        onValueChange = { input = it },
                        modifier = Modifier.weight(1f),
                        placeholder = { Text("가고 싶은 여행지를 물어보세요") },
                        enabled = !isSending
                    )

                    Spacer(modifier = Modifier.width(8.dp))

                    Button(
                        onClick = {
                            viewModel.sendMessage(input)
                            input = ""
                        },
                        enabled = !isSending && input.isNotBlank()
                    ) {
                        Text("전송")
                    }
                }
            }
        }
    ) { paddingValues ->

        if (messages.isEmpty()) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .padding(16.dp),
                contentAlignment = Alignment.Center
            ) {
                Text("가고 싶은 여행지나 원하는 여행 스타일을 물어보세요.")
            }
        } else {
            LazyColumn(
                state = listState,
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .padding(horizontal = 16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
                contentPadding = PaddingValues(vertical = 12.dp)
            ) {
                items(messages) { message ->
                    when (message) {
                        is ChatMessage.UserMessage -> ChatBubble(text = message.text, isUser = true)
                        is ChatMessage.AiMessage -> ChatBubble(text = message.text, isUser = false)
                        is ChatMessage.RecommendationCard -> RecommendationCardView(
                            card = message,
                            onAddClick = { dialogCard = message }
                        )
                    }
                }

                if (isSending) {
                    item {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            CircularProgressIndicator(modifier = Modifier.size(20.dp))
                            Spacer(modifier = Modifier.width(8.dp))
                            Text("AI가 추천을 준비하고 있어요...")
                        }
                    }
                }
            }
        }
    }

    dialogCard?.let { card ->
        AddToScheduleDialog(
            placeName = card.placeName,
            initialStartTime = card.suggestedStartTime,
            initialEndTime = card.suggestedEndTime,
            onDismiss = { dialogCard = null },
            onConfirm = { startTime, endTime ->
                viewModel.addToSchedule(card, startTime, endTime) { success, errorMessage ->
                    dialogCard = null
                    Toast.makeText(
                        context,
                        if (success) "일정에 추가되었습니다." else (errorMessage ?: "일정 추가에 실패했습니다."),
                        Toast.LENGTH_SHORT
                    ).show()
                }
            }
        )
    }
}

@Composable
fun ChatBubble(text: String, isUser: Boolean) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = if (isUser) Arrangement.End else Arrangement.Start
    ) {
        Surface(
            color = if (isUser) {
                MaterialTheme.colorScheme.primaryContainer
            } else {
                MaterialTheme.colorScheme.surfaceVariant
            },
            shape = MaterialTheme.shapes.medium,
            modifier = Modifier.widthIn(max = 280.dp)
        ) {
            Text(
                text = text,
                modifier = Modifier.padding(12.dp)
            )
        }
    }
}

@Composable
fun RecommendationCardView(
    card: ChatMessage.RecommendationCard,
    onAddClick: () -> Unit
) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(160.dp)
            ) {
                if (card.photoUrl != null) {
                    AsyncImage(
                        model = card.photoUrl,
                        contentDescription = card.placeName,
                        modifier = Modifier.fillMaxSize(),
                        contentScale = ContentScale.Crop
                    )
                } else {
                    Box(
                        modifier = Modifier
                            .fillMaxSize()
                            .background(MaterialTheme.colorScheme.surfaceVariant),
                        contentAlignment = Alignment.Center
                    ) {
                        if (card.photoLoading) {
                            CircularProgressIndicator()
                        } else {
                            Text(
                                text = "사진 없음",
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                }
            }

            Column(modifier = Modifier.padding(16.dp)) {
                Text(
                    text = card.placeName,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )

                Spacer(modifier = Modifier.height(4.dp))

                Text(
                    text = card.description,
                    style = MaterialTheme.typography.bodyMedium
                )

                Spacer(modifier = Modifier.height(12.dp))

                if (card.addedToSchedule) {
                    Button(onClick = {}, enabled = false) {
                        Text("추가됨")
                    }
                } else {
                    Button(onClick = onAddClick) {
                        Text("일정에 추가")
                    }
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AddToScheduleDialog(
    placeName: String,
    initialStartTime: String,
    initialEndTime: String,
    onDismiss: () -> Unit,
    onConfirm: (startTime: String, endTime: String) -> Unit
) {
    // 서버 시간 형식은 "yyyy-MM-ddTHH:mm:ssZ" 이므로 날짜는 고정하고 시:분만 수정할 수 있게 한다.
    val startDate = initialStartTime.take(10)
    val endDate = initialEndTime.take(10)

    val startHour = initialStartTime.substringAfter('T').take(2).toIntOrNull() ?: 10
    val startMinute = initialStartTime.substringAfter('T').drop(3).take(2).toIntOrNull() ?: 0
    val endHour = initialEndTime.substringAfter('T').take(2).toIntOrNull() ?: 12
    val endMinute = initialEndTime.substringAfter('T').drop(3).take(2).toIntOrNull() ?: 0

    val startTimeState = rememberTimePickerState(
        initialHour = startHour,
        initialMinute = startMinute,
        is24Hour = true
    )

    val endTimeState = rememberTimePickerState(
        initialHour = endHour,
        initialMinute = endMinute,
        is24Hour = true
    )

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("$placeName 일정 추가") },
        text = {
            Column(modifier = Modifier.verticalScroll(rememberScrollState())) {
                Text("방문 시작 시간 ($startDate)", style = MaterialTheme.typography.labelLarge)
                TimeInput(state = startTimeState)

                Spacer(modifier = Modifier.height(16.dp))

                Text("방문 종료 시간 ($endDate)", style = MaterialTheme.typography.labelLarge)
                TimeInput(state = endTimeState)
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    val startTime = "%sT%02d:%02d:00Z".format(
                        startDate, startTimeState.hour, startTimeState.minute
                    )
                    val endTime = "%sT%02d:%02d:00Z".format(
                        endDate, endTimeState.hour, endTimeState.minute
                    )
                    onConfirm(startTime, endTime)
                }
            ) {
                Text("확인")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("취소")
            }
        }
    )
}

@Preview(showBackground = true)
@Composable
fun RecommendationCardPreview() {
    RecommendationCardView(
        card = ChatMessage.RecommendationCard(
            id = "preview",
            placeName = "경복궁",
            description = "조선 왕조의 정궁으로, 한복을 입고 산책하기 좋은 대표 명소예요.",
            suggestedStartTime = "2026-09-10T10:00:00Z",
            suggestedEndTime = "2026-09-10T12:00:00Z",
            photoUrl = null,
            photoLoading = false
        ),
        onAddClick = {}
    )
}
