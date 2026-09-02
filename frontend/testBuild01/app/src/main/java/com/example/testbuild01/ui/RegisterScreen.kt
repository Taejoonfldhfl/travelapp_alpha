package com.example.testbuild01.ui

import android.util.Log
import android.widget.Toast
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.model.RegisterRequest
import com.example.testbuild01.data.model.RegisterResponse
import com.example.testbuild01.data.network.RetrofitClient

import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

@Composable
fun RegisterScreen(onRegisterSuccess: () -> Unit) {
    var username by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var nickname by remember { mutableStateOf("") }

    val context = LocalContext.current
    val apiService = RetrofitClient.instance

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        Text(text = "회원가입", style = MaterialTheme.typography.headlineMedium)
        Spacer(modifier = Modifier.height(32.dp))

        OutlinedTextField(
            value = nickname,
            onValueChange = { nickname = it },
            label = { Text("닉네임") },
            modifier = Modifier.fillMaxWidth()
        )
        Spacer(modifier = Modifier.height(24.dp))

        OutlinedTextField(
            value = username,
            onValueChange = { username = it },
            label = { Text("아이디") },
            modifier = Modifier.fillMaxWidth()
        )
        Spacer(modifier = Modifier.height(8.dp))

        OutlinedTextField(
            value = password,
            onValueChange = { password = it },
            label = { Text("비밀번호") },
            visualTransformation = PasswordVisualTransformation(),
            modifier = Modifier.fillMaxWidth()
        )
        Spacer(modifier = Modifier.height(24.dp))


        Button(
            onClick = {
                // 1. 서버로 보낼 데이터 묶기
                val registerData = RegisterRequest(
                    email = username,
                    password = password,
                    nickname = nickname
                )

                // 2. 서버 통신 시작
                apiService.register(registerData).enqueue(object : Callback<RegisterResponse> {
                    override fun onResponse(call: Call<RegisterResponse>, response: Response<RegisterResponse>) {
                        if (response.isSuccessful) {
                            val userInfo = response.body()
                            Log.d("API", "회원가입 완료된 이메일: ${userInfo?.email}")
                            Log.d("API", "발급받은 토큰: ${userInfo?.token}")

                            Toast.makeText(context, "회원가입 성공", Toast.LENGTH_SHORT).show()
                            onRegisterSuccess() // 로그인 화면으로 이동
                        } else {
                            val errorMsg = when(response.code()) {
                                400 -> "이미 존재하는 이메일이거나 형식이 잘못되었습니다."
                                else -> "회원가입 실패 (코드: ${response.code()})"
                            }
                            Toast.makeText(context, errorMsg, Toast.LENGTH_SHORT).show()
                        }
                    }

                    override fun onFailure(call: Call<RegisterResponse>, t: Throwable) {
                        Toast.makeText(context, "서버 연결 실패", Toast.LENGTH_SHORT).show()
                        Log.e("API", "네트워크 에러: ${t.message}")
                    }
                })
            },
            modifier = Modifier.fillMaxWidth()
        ) {
            Text("확인")
        }

    }
}

@Preview(showBackground = true)
@Composable
fun RegisterScreenPreview() {
    RegisterScreen(
        onRegisterSuccess = {},
    )
}
