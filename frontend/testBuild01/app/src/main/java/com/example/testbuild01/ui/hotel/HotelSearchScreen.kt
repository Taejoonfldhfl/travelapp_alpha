@file:OptIn(ExperimentalMaterial3Api::class)

package com.example.testbuild01.ui.hotel

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.hotel.HotelSearchResult
import com.example.testbuild01.data.hotel.IHotelSearchProvider
import kotlinx.coroutines.launch

/** Mock 제공자로 호텔 후보를 검색해 비교하고, 실제 예약은 아고다 딥링크(Custom Tabs)로 넘긴다. */
@Composable
fun HotelSearchScreen(
    onBack: () -> Unit,
    searchProvider: IHotelSearchProvider = remember { com.example.testbuild01.data.hotel.MockHotelSearchProvider() }
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()

    var query by remember { mutableStateOf("") }
    var results by remember { mutableStateOf<List<HotelSearchResult>>(emptyList()) }
    var loading by remember { mutableStateOf(false) }
    var searched by remember { mutableStateOf(false) }

    fun search() {
        loading = true
        scope.launch {
            // 체크인/체크아웃 날짜는 Amadeus 연동 전까지는 Mock이 무시하므로 지금은 오늘/내일로 고정한다.
            val now = System.currentTimeMillis()
            results = searchProvider.searchHotels(query, now, now + 24 * 60 * 60 * 1000)
            loading = false
            searched = true
        }
    }

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text("호텔 검색") },
                navigationIcon = { TextButton(onClick = onBack) { Text("뒤로") } }
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            OutlinedTextField(
                value = query,
                onValueChange = { query = it },
                label = { Text("여행지 / 호텔명") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )
            Button(onClick = ::search, modifier = Modifier.fillMaxWidth()) { Text("검색") }

            when {
                loading -> Box(modifier = Modifier.fillMaxSize()) {
                    CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
                }
                searched && results.isEmpty() -> Text("검색 결과가 없습니다.")
                else -> LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    items(results) { result ->
                        HotelSearchResultCard(
                            result = result,
                            onBookOnAgoda = { AgodaDeepLink.open(context, result.hotelName) }
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun HotelSearchResultCard(result: HotelSearchResult, onBookOnAgoda: () -> Unit) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
            Text(result.hotelName, style = MaterialTheme.typography.titleLarge)
            Text(result.address, style = MaterialTheme.typography.bodyMedium)
            Text(result.priceRangeLabel, style = MaterialTheme.typography.bodyMedium)
            Button(onClick = onBookOnAgoda, modifier = Modifier.fillMaxWidth()) {
                Text("예약 사이트에서 예약하기")
            }
        }
    }
}
