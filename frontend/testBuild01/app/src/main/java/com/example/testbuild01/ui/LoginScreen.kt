package com.example.testbuild01.ui

import android.util.Log
import android.widget.Toast
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.tooling.preview.Preview
import com.example.testbuild01.data.local.TokenManager

import com.example.testbuild01.data.model.LoginRequest
import com.example.testbuild01.data.model.LoginResponse
import com.example.testbuild01.data.network.RetrofitClient

import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

@Composable
fun LoginScreen(onLoginSuccess: () -> Unit,
                onRegisterSelected: () -> Unit) {
    var username by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }

    val context = LocalContext.current
    val apiService = RetrofitClient.instance
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        Text(text = "로그인", style = MaterialTheme.typography.headlineMedium)
        Spacer(modifier = Modifier.height(32.dp))
        
        OutlinedTextField(
            value = username,
            onValueChange = { username = it },
            label = { Text("이메일") },
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
                // 로그인 요청 데이터 생성
                val loginData = LoginRequest(email = username, password = password)

                // 서버 호출 시작
                apiService.login(loginData).enqueue(object : Callback<LoginResponse> {
                    override fun onResponse(call: Call<LoginResponse>, response: Response<LoginResponse>) {
                        if (response.isSuccessful) {
                            val loginResponse = response.body()

                            if (loginResponse != null) {
                                val tokenManager = TokenManager(context)
                                tokenManager.saveToken(loginResponse.token)

                                Toast.makeText(
                                    context,
                                    "로그인 성공",
                                    Toast.LENGTH_SHORT
                                ).show()

                                Log.d("API", "로그인 성공")
                                Log.d("API", "토큰 저장 완료")

                                onLoginSuccess()
                            }
                        } else {
                            val message = when(response.code()) {
                                401 -> "이메일 또는 비밀번호가 틀렸습니다."
                                400 -> "입력 형식이 잘못되었습니다."
                                else -> "로그인 실패 (에러 코드: ${response.code()})"
                            }
                            Toast.makeText(context, message, Toast.LENGTH_SHORT).show()
                            Log.e("API", "실패 상세: ${response.errorBody()?.string()}")
                        }
                    }

                    override fun onFailure(call: Call<LoginResponse>, t: Throwable) {
                        Toast.makeText(context, "서비 연결에 실패. 네트워크를 확인해주세요.", Toast.LENGTH_LONG).show()
                        Log.e("API", "네트워크 에러: ${t.message}")
                    }
                })
            },
            modifier = Modifier.fillMaxWidth()
        ) {
            Text("로그인")
        }
        Spacer(modifier = Modifier.height(8.dp))

        Button(
            onClick = onRegisterSelected,
            modifier = Modifier.fillMaxWidth()
        ) {
            Text("회원가입")
        }
    }
}

@Preview(showBackground = true)
@Composable
fun LoginScreenPreview() {
    LoginScreen(
        onLoginSuccess = {},
        onRegisterSelected = {}
        )
}
