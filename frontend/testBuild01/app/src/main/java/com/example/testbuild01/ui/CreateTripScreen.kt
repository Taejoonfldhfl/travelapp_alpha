package com.example.testbuild01.ui

import android.widget.Toast
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.model.TripCreateRequest
import com.example.testbuild01.data.model.TripResponse
import com.example.testbuild01.data.network.RetrofitClient
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

@Composable
fun CreateTripScreen(
    onTripCreated: () -> Unit
) {
    var title by remember { mutableStateOf("") }
    var startDate by remember { mutableStateOf("") }
    var endDate by remember { mutableStateOf("") }

    val context = LocalContext.current

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {

        Spacer(modifier = Modifier.height(50.dp))

        Text(
            text = "새 여행 만들기",
            style = MaterialTheme.typography.headlineMedium
        )

        Spacer(modifier = Modifier.height(30.dp))

        OutlinedTextField(
            value = title,
            onValueChange = { title = it },
            label = { Text("여행 제목") },
            modifier = Modifier.fillMaxWidth()
        )

        Spacer(modifier = Modifier.height(12.dp))

        OutlinedTextField(
            value = startDate,
            onValueChange = { startDate = it },
            label = { Text("시작일 (예: 2026-09-10)") },
            modifier = Modifier.fillMaxWidth()
        )

        Spacer(modifier = Modifier.height(12.dp))

        OutlinedTextField(
            value = endDate,
            onValueChange = { endDate = it },
            label = { Text("종료일 (예: 2026-09-13)") },
            modifier = Modifier.fillMaxWidth()
        )

        Spacer(modifier = Modifier.height(24.dp))

        Button(
            onClick = {

                if (
                    title.isBlank() ||
                    startDate.isBlank() ||
                    endDate.isBlank()
                ) {
                    Toast.makeText(
                        context,
                        "모든 항목을 입력해주세요.",
                        Toast.LENGTH_SHORT
                    ).show()

                    return@Button
                }

                val request = TripCreateRequest(
                    title = title,
                    startDate = "${startDate}T00:00:00Z",
                    endDate = "${endDate}T00:00:00Z"
                )

                RetrofitClient.instance
                    .createTrip(request)
                    .enqueue(object : Callback<TripResponse> {

                        override fun onResponse(
                            call: Call<TripResponse>,
                            response: Response<TripResponse>
                        ) {
                            if (response.isSuccessful) {

                                Toast.makeText(
                                    context,
                                    "여행이 생성되었습니다.",
                                    Toast.LENGTH_SHORT
                                ).show()

                                onTripCreated()

                            } else {

                                Toast.makeText(
                                    context,
                                    "여행 생성 실패 (${response.code()})",
                                    Toast.LENGTH_SHORT
                                ).show()
                            }
                        }

                        override fun onFailure(
                            call: Call<TripResponse>,
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
            Text("여행 생성")
        }
    }
}