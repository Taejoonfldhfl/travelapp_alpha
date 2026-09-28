package com.example.testbuild01.ui.aichat

import android.Manifest
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
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
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Place
import coil.compose.SubcomposeAsyncImage

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AiChatScreen(
    tripId: Int,
    onBack: () -> Unit
) {
    val context = LocalContext.current
    val viewModel: AiChatViewModel = viewModel(
        factory = AiChatViewModelFactory(tripId, context.applicationContext)
    )

    val messages by viewModel.messages.collectAsState()
    val isSending by viewModel.isSending.collectAsState()
    val showLocationConsentDialog by viewModel.showLocationConsentDialog.collectAsState()
    val listState = rememberLazyListState()

    var input by remember { mutableStateOf("") }
    var dialogCard by remember { mutableStateOf<ChatMessage.RecommendationCard?>(null) }

    // 규칙 1: 사용자가 위치 정보 수집에 동의했는데 아직 시스템 권한이 없으면 ViewModel이
    // 이 이벤트를 보내고, 여기서 실제 런타임 권한 다이얼로그를 띄운다 (MapScreen과 동일한 방식).
    val locationPermissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestMultiplePermissions()
    ) { permissions ->
        val granted = permissions.getOrDefault(Manifest.permission.ACCESS_FINE_LOCATION, false) ||
            permissions.getOrDefault(Manifest.permission.ACCESS_COARSE_LOCATION, false)
        viewModel.onLocationPermissionResult(granted)
    }

    LaunchedEffect(viewModel) {
        viewModel.locationPermissionRequests.collect {
            locationPermissionLauncher.launch(
                arrayOf(
                    Manifest.permission.ACCESS_FINE_LOCATION,
                    Manifest.permission.ACCESS_COARSE_LOCATION
                )
            )
        }
    }

    if (showLocationConsentDialog) {
        LocationConsentDialog(
            onConfirm = { viewModel.onLocationConsentResult(true) },
            onDismiss = { viewModel.onLocationConsentResult(false) }
        )
    }

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

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LocationConsentDialog(
    onConfirm: () -> Unit,
    onDismiss: () -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("위치 정보 수집 안내") },
        text = {
            Text(
                "'근처' 검색을 위해 기기의 현재 위치(GPS)를 확인해 사용합니다.\n" +
                    "수집한 위치 정보는 주변 장소를 추천하는 용도로만 사용되며 저장되지 않습니다.\n" +
                    "계속하시겠습니까?"
            )
        },
        confirmButton = {
            Button(onClick = onConfirm) {
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

// 추천 카드 사진 영역의 대체 표시: 사진을 조회 중이면 로딩, 사진이 없거나 불러오지 못했으면 장소 아이콘 + "사진 없음".
@Composable
private fun PlacePhotoPlaceholder(loading: Boolean) {
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.surfaceVariant),
        contentAlignment = Alignment.Center
    ) {
        if (loading) {
            CircularProgressIndicator()
        } else {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Icon(
                    imageVector = Icons.Filled.Place,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(40.dp)
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = "사진 없음",
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
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
                when {
                    card.photoUrl != null -> SubcomposeAsyncImage(
                        model = card.photoUrl,
                        contentDescription = card.placeName,
                        modifier = Modifier.fillMaxSize(),
                        contentScale = ContentScale.Crop,
                        loading = { PlacePhotoPlaceholder(loading = true) },
                        // URL은 있는데 이미지를 못 불러온 경우(만료/깨진 링크)도 "사진 없음"으로 보여준다.
                        error = { PlacePhotoPlaceholder(loading = false) }
                    )
                    else -> PlacePhotoPlaceholder(loading = card.photoLoading)
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
