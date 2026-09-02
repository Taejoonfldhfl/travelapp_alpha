package com.example.testbuild01.ui

import android.util.Log
import android.widget.Toast
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.model.TripMemberAddRequest
import com.example.testbuild01.data.model.TripMemberResponse
import com.example.testbuild01.data.network.RetrofitClient
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TripMemberScreen(
    tripId: Int,
    onBack: () -> Unit
) {
    val context = LocalContext.current

    var members by remember {
        mutableStateOf<List<TripMemberResponse>>(emptyList())
    }

    var email by remember {
        mutableStateOf("")
    }

    var isLoading by remember {
        mutableStateOf(true)
    }

    fun loadMembers() {

        isLoading = true

        RetrofitClient.instance
            .getTripMembers(tripId)
            .enqueue(object : Callback<List<TripMemberResponse>> {

                override fun onResponse(
                    call: Call<List<TripMemberResponse>>,
                    response: Response<List<TripMemberResponse>>
                ) {
                    isLoading = false

                    if (response.isSuccessful) {
                        members = response.body() ?: emptyList()

                        Log.d("Member_API", "응답: ${response.body()}")
                    } else {
                        Toast.makeText(
                            context,
                            "멤버 조회 실패 (${response.code()})",
                            Toast.LENGTH_SHORT
                        ).show()
                    }
                }

                override fun onFailure(
                    call: Call<List<TripMemberResponse>>,
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
        loadMembers()
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("여행 멤버") },
                navigationIcon = {
                    TextButton(
                        onClick = onBack
                    ) {
                        Text("뒤로")
                    }
                }
            )
        }
    ) { paddingValues ->

        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp)
        ) {

            Text(
                text = "멤버 추가",
                style = MaterialTheme.typography.titleMedium
            )

            Spacer(modifier = Modifier.height(8.dp))

            OutlinedTextField(
                value = email,
                onValueChange = { email = it },
                label = { Text("추가할 사용자 이메일") },
                modifier = Modifier.fillMaxWidth()
            )

            Spacer(modifier = Modifier.height(8.dp))

            Button(
                onClick = {

                    if (email.isBlank()) {
                        Toast.makeText(
                            context,
                            "이메일을 입력해주세요.",
                            Toast.LENGTH_SHORT
                        ).show()

                        return@Button
                    }

                    val request =
                        TripMemberAddRequest(email)

                    RetrofitClient.instance
                        .addTripMember(
                            tripId,
                            request
                        )
                        .enqueue(object : Callback<Void> {

                            override fun onResponse(
                                call: Call<Void>,
                                response: Response<Void>
                            ) {

                                if (response.isSuccessful) {

                                    Toast.makeText(
                                        context,
                                        "멤버가 추가되었습니다.",
                                        Toast.LENGTH_SHORT
                                    ).show()

                                    email = ""

                                    loadMembers()

                                } else {

                                    val message =
                                        when (response.code()) {

                                            400 ->
                                                "이미 참여 중인 사용자입니다."

                                            403 ->
                                                "Owner만 멤버를 추가할 수 있습니다."

                                            404 ->
                                                "사용자를 찾을 수 없습니다."

                                            else ->
                                                "멤버 추가 실패 (${response.code()})"
                                        }

                                    Toast.makeText(
                                        context,
                                        message,
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
                },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("멤버 추가")
            }

            Spacer(modifier = Modifier.height(24.dp))

            Text(
                text = "현재 멤버",
                style = MaterialTheme.typography.titleMedium
            )

            Spacer(modifier = Modifier.height(8.dp))

            if (isLoading) {

                CircularProgressIndicator()

            } else {

                LazyColumn(
                    verticalArrangement =
                        Arrangement.spacedBy(8.dp)
                ) {

                    items(members) { member ->

                        Card(
                            modifier =
                                Modifier.fillMaxWidth()
                        ) {

                            Column(
                                modifier =
                                    Modifier.padding(16.dp)
                            ) {

                                Text(
                                    text = member.nickname ?: "닉네임 없음",
                                    style =
                                        MaterialTheme.typography.titleMedium
                                )

                                Text(member.email ?: "이메일 없음")

                                Text(
                                    text = "권한: ${member.role ?: "Member"}"
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}