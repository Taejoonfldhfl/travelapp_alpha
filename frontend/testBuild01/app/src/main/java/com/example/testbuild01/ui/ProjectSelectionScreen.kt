package com.example.testbuild01.ui

import android.widget.Toast
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.model.TripResponse
import com.example.testbuild01.data.network.AuthSession
import com.example.testbuild01.data.network.RetrofitClient
import kotlinx.coroutines.launch
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProjectSelectionScreen(
    onProjectSelected: (TripResponse) -> Unit,
    onCreateTripSelected: () -> Unit,
    onTicketsSelected: () -> Unit = {},
    onLogout: () -> Unit = {}
) {
    val context = LocalContext.current
    val coroutineScope = rememberCoroutineScope()

    var trips by remember {
        mutableStateOf<List<TripResponse>>(emptyList())
    }

    var isLoading by remember {
        mutableStateOf(true)
    }

    LaunchedEffect(Unit) {

        RetrofitClient.instance
            .getTrips()
            .enqueue(object : Callback<List<TripResponse>> {

                override fun onResponse(
                    call: Call<List<TripResponse>>,
                    response: Response<List<TripResponse>>
                ) {
                    isLoading = false

                    if (response.isSuccessful) {
                        trips = response.body() ?: emptyList()
                    } else {
                        Toast.makeText(
                            context,
                            "여행 목록 조회 실패 (${response.code()})",
                            Toast.LENGTH_SHORT
                        ).show()
                    }
                }

                override fun onFailure(
                    call: Call<List<TripResponse>>,
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

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text("여행 프로젝트 선택") },
                actions = {
                    TextButton(onClick = onTicketsSelected) {
                        Text("티켓")
                    }
                    TextButton(onClick = {
                        coroutineScope.launch {
                            AuthSession.logout(context)
                            onLogout()
                        }
                    }) {
                        Text("로그아웃")
                    }
                }
            )
        },

        floatingActionButton = {
            FloatingActionButton(
                onClick = onCreateTripSelected
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

        } else {

            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .padding(16.dp),
                verticalArrangement =
                    Arrangement.spacedBy(8.dp)
            ) {

                items(trips) { trip ->

                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clickable {
                                onProjectSelected(trip)
                            }
                    ) {
                        Column(
                            modifier = Modifier.padding(16.dp)
                        ) {

                            Text(
                                text = trip.title,
                                style =
                                    MaterialTheme.typography.titleLarge
                            )

                            Text(
                                text =
                                    "${trip.startDate} ~ ${trip.endDate}"
                            )
                        }
                    }
                }
            }
        }
    }
}