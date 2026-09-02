package com.example.testbuild01.ui

import android.widget.Toast
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.model.ScheduleCreateRequest
import com.example.testbuild01.data.model.ScheduleResponse
import com.example.testbuild01.data.network.RetrofitClient
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ScheduleScreen(
    tripId: Int,
    onBack: () -> Unit
) {
    val context = LocalContext.current

    var schedules by remember {
        mutableStateOf<List<ScheduleResponse>>(emptyList())
    }

    var showAddDialog by remember {
        mutableStateOf(false)
    }

    var isLoading by remember {
        mutableStateOf(true)
    }

    fun loadSchedules() {
        isLoading = true

        RetrofitClient.instance
            .getSchedules(tripId)
            .enqueue(object : Callback<List<ScheduleResponse>> {

                override fun onResponse(
                    call: Call<List<ScheduleResponse>>,
                    response: Response<List<ScheduleResponse>>
                ) {
                    isLoading = false

                    if (response.isSuccessful) {
                        schedules = response.body() ?: emptyList()
                    } else {
                        Toast.makeText(
                            context,
                            "일정 조회 실패 (${response.code()})",
                            Toast.LENGTH_SHORT
                        ).show()
                    }
                }

                override fun onFailure(
                    call: Call<List<ScheduleResponse>>,
                    t: Throwable
                ) {
                    isLoading = false

                    Toast.makeText(
                        context,
                        "서버 연결 실패",
                        Toast.LENGTH_SHORT
                    ).show()
                }
            })
    }

    LaunchedEffect(tripId) {
        loadSchedules()
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("여행 일정") },

                navigationIcon = {
                    TextButton(onClick = onBack) {
                        Text("뒤로")
                    }
                }
            )
        },

        floatingActionButton = {
            FloatingActionButton(
                onClick = {
                    showAddDialog = true
                }
            ) {
                Text("+")
            }
        }
    ) { paddingValues ->

        if (isLoading) {

            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
            ) {
                CircularProgressIndicator()
            }

        } else if (schedules.isEmpty()) {

            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .padding(16.dp)
            ) {
                Text("등록된 일정이 없습니다.")
            }

        } else {

            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .padding(16.dp),

                verticalArrangement =
                    Arrangement.spacedBy(12.dp)
            ) {

                items(schedules) { schedule ->

                    ScheduleItem(
                        tripId = tripId,
                        schedule = schedule,

                        onDeleted = {
                            loadSchedules()
                        }
                    )
                }
            }
        }
    }

    if (showAddDialog) {

        AddScheduleDialog(
            tripId = tripId,

            onDismiss = {
                showAddDialog = false
            },

            onCreated = {
                showAddDialog = false
                loadSchedules()
            }
        )
    }
}

@Composable
fun ScheduleItem(
    tripId: Int,
    schedule: ScheduleResponse,
    onDeleted: () -> Unit
) {
    val context = LocalContext.current

    Card(
        modifier = Modifier.fillMaxWidth()
    ) {

        Column(
            modifier = Modifier.padding(16.dp)
        ) {

            Text(
                text = schedule.title,
                style = MaterialTheme.typography.titleLarge
            )

            Spacer(
                modifier = Modifier.height(4.dp)
            )

            if (schedule.placeName.isNotBlank()) {
                Text("장소: ${schedule.placeName}")
            }

            Text(
                text = "${schedule.startTime} ~ ${schedule.endTime}"
            )

            if (schedule.description.isNotBlank()) {

                Spacer(
                    modifier = Modifier.height(4.dp)
                )

                Text(schedule.description)
            }

            Spacer(
                modifier = Modifier.height(12.dp)
            )

            Button(
                onClick = {

                    RetrofitClient.instance
                        .deleteSchedule(
                            tripId,
                            schedule.id
                        )
                        .enqueue(object : Callback<Void> {

                            override fun onResponse(
                                call: Call<Void>,
                                response: Response<Void>
                            ) {

                                if (response.isSuccessful) {

                                    Toast.makeText(
                                        context,
                                        "일정이 삭제되었습니다.",
                                        Toast.LENGTH_SHORT
                                    ).show()

                                    onDeleted()

                                } else {

                                    Toast.makeText(
                                        context,
                                        "삭제 실패 (${response.code()})",
                                        Toast.LENGTH_SHORT
                                    ).show()
                                }
                            }

                            override fun onFailure(
                                call: Call<Void>,
                                t: Throwable
                            ) {

                                Toast.makeText(
                                    context,
                                    "서버 연결 실패",
                                    Toast.LENGTH_SHORT
                                ).show()
                            }
                        })
                }
            ) {
                Text("삭제")
            }
        }
    }
}

@Composable
fun AddScheduleDialog(
    tripId: Int,
    onDismiss: () -> Unit,
    onCreated: () -> Unit
) {
    val context = LocalContext.current

    var title by remember { mutableStateOf("") }
    var placeName by remember { mutableStateOf("") }
    var description by remember { mutableStateOf("") }

    var startTime by remember {
        mutableStateOf("")
    }

    var endTime by remember {
        mutableStateOf("")
    }

    AlertDialog(
        onDismissRequest = onDismiss,

        title = {
            Text("일정 추가")
        },

        text = {

            Column {

                OutlinedTextField(
                    value = title,
                    onValueChange = { title = it },
                    label = {
                        Text("일정 제목")
                    },
                    modifier = Modifier.fillMaxWidth()
                )

                Spacer(
                    modifier = Modifier.height(8.dp)
                )

                OutlinedTextField(
                    value = placeName,
                    onValueChange = { placeName = it },
                    label = {
                        Text("장소")
                    },
                    modifier = Modifier.fillMaxWidth()
                )

                Spacer(
                    modifier = Modifier.height(8.dp)
                )

                OutlinedTextField(
                    value = description,
                    onValueChange = { description = it },
                    label = {
                        Text("메모")
                    },
                    modifier = Modifier.fillMaxWidth()
                )

                Spacer(
                    modifier = Modifier.height(8.dp)
                )

                OutlinedTextField(
                    value = startTime,
                    onValueChange = { startTime = it },
                    label = {
                        Text("시작: 2026-09-10T10:00:00Z")
                    },
                    modifier = Modifier.fillMaxWidth()
                )

                Spacer(
                    modifier = Modifier.height(8.dp)
                )

                OutlinedTextField(
                    value = endTime,
                    onValueChange = { endTime = it },
                    label = {
                        Text("종료: 2026-09-10T12:00:00Z")
                    },
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },

        confirmButton = {

            Button(
                onClick = {

                    if (
                        title.isBlank() ||
                        startTime.isBlank() ||
                        endTime.isBlank()
                    ) {

                        Toast.makeText(
                            context,
                            "제목과 시간을 입력해주세요.",
                            Toast.LENGTH_SHORT
                        ).show()

                        return@Button
                    }

                    val request =
                        ScheduleCreateRequest(
                            title = title,
                            placeName = placeName,
                            description = description,
                            startTime = startTime,
                            endTime = endTime,
                            order = 0
                        )

                    RetrofitClient.instance
                        .createSchedule(
                            tripId,
                            request
                        )
                        .enqueue(object :
                            Callback<ScheduleResponse> {

                            override fun onResponse(
                                call: Call<ScheduleResponse>,
                                response: Response<ScheduleResponse>
                            ) {

                                if (response.isSuccessful) {

                                    Toast.makeText(
                                        context,
                                        "일정이 추가되었습니다.",
                                        Toast.LENGTH_SHORT
                                    ).show()

                                    onCreated()

                                } else {

                                    val errorBody =
                                        response.errorBody()
                                            ?.string()

                                    Toast.makeText(
                                        context,
                                        "일정 추가 실패 (${response.code()})\n$errorBody",
                                        Toast.LENGTH_LONG
                                    ).show()
                                }
                            }

                            override fun onFailure(
                                call: Call<ScheduleResponse>,
                                t: Throwable
                            ) {

                                Toast.makeText(
                                    context,
                                    "서버 연결 실패",
                                    Toast.LENGTH_SHORT
                                ).show()
                            }
                        })
                }
            ) {
                Text("추가")
            }
        },

        dismissButton = {

            TextButton(
                onClick = onDismiss
            ) {
                Text("취소")
            }
        }
    )
}