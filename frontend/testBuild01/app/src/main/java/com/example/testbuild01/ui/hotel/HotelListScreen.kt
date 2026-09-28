@file:OptIn(ExperimentalMaterial3Api::class)

package com.example.testbuild01.ui.hotel

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.widget.Toast
import androidx.browser.customtabs.CustomTabsIntent
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Home
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import coil.compose.SubcomposeAsyncImage
import com.example.testbuild01.data.model.HotelInfoItem
import com.google.android.gms.maps.CameraUpdateFactory
import com.google.android.gms.maps.model.LatLng
import com.google.android.gms.maps.model.LatLngBounds
import com.google.maps.android.compose.GoogleMap
import com.google.maps.android.compose.MapUiSettings
import com.google.maps.android.compose.Marker
import com.google.maps.android.compose.rememberCameraPositionState
import com.google.maps.android.compose.rememberUpdatedMarkerState

/**
 * 숙박시설 정보 목록(TourAPI). 정보 조회와 외부 예약 사이트 이동까지만 한다 — 가격·잔여객실·예약·결제는 없다.
 * 좌표가 있는 숙소는 위쪽 지도에 마커로도 표시하고, 좌표가 없는 숙소는 목록에만 보인다.
 */
@Composable
fun HotelListScreen(
    onBack: () -> Unit,
    viewModel: HotelListViewModel = viewModel(factory = HotelListViewModelFactory())
) {
    val state by viewModel.state.collectAsState()
    val context = LocalContext.current
    val focusManager = LocalFocusManager.current

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text("숙소 찾기") },
                navigationIcon = { TextButton(onClick = onBack) { Text("뒤로") } }
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            SearchControls(
                state = state,
                onModeChange = viewModel::onModeChange,
                onKeywordChange = viewModel::onKeywordChange,
                onSearchKeyword = {
                    focusManager.clearFocus()
                    viewModel.searchByKeyword()
                },
                onAreaSelected = viewModel::searchByArea
            )

            when (val content = state.content) {
                HotelListContent.Idle -> CenteredMessage("키워드나 지역으로 숙소를 찾아보세요.")
                HotelListContent.Loading -> Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator()
                }
                is HotelListContent.Error -> CenteredMessage(content.message, actionLabel = "다시 시도", onAction = viewModel::retry)
                HotelListContent.Empty -> CenteredMessage("조건에 맞는 숙소가 없어요. 다른 키워드나 지역으로 찾아보세요.")
                is HotelListContent.Results -> HotelResults(content, onHotelClick = viewModel::onHotelSelected)
            }
        }
    }

    state.selectedHotel?.let { hotel ->
        BookingSiteSheet(
            hotel = hotel,
            onDismiss = viewModel::onBookingSheetDismissed,
            onOpenSite = { site ->
                openInBrowser(context, HotelBookingLinkBuilder.searchUrl(site, hotel.name))
                viewModel.onBookingSheetDismissed()
            }
        )
    }
}

@Composable
private fun SearchControls(
    state: HotelListUiState,
    onModeChange: (HotelSearchMode) -> Unit,
    onKeywordChange: (String) -> Unit,
    onSearchKeyword: () -> Unit,
    onAreaSelected: (TourApiArea) -> Unit
) {
    Column(
        modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            FilterChip(
                selected = state.mode == HotelSearchMode.KEYWORD,
                onClick = { onModeChange(HotelSearchMode.KEYWORD) },
                label = { Text("키워드") }
            )
            FilterChip(
                selected = state.mode == HotelSearchMode.AREA,
                onClick = { onModeChange(HotelSearchMode.AREA) },
                label = { Text("지역") }
            )
        }

        when (state.mode) {
            HotelSearchMode.KEYWORD -> Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                OutlinedTextField(
                    value = state.keyword,
                    onValueChange = onKeywordChange,
                    label = { Text("숙소 이름, 지역명") },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                    keyboardActions = KeyboardActions(onSearch = { if (state.canSearchKeyword) onSearchKeyword() }),
                    modifier = Modifier.weight(1f)
                )
                Button(onClick = onSearchKeyword, enabled = state.canSearchKeyword) { Text("검색") }
            }

            HotelSearchMode.AREA -> LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                items(TOUR_API_AREAS, key = { it.code }) { area ->
                    FilterChip(
                        selected = state.selectedArea == area,
                        onClick = { onAreaSelected(area) },
                        label = { Text(area.name) }
                    )
                }
            }
        }
    }
}

@Composable
private fun HotelResults(content: HotelListContent.Results, onHotelClick: (HotelInfoItem) -> Unit) {
    val mappable = content.mappable

    LazyColumn(
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, bottom = 16.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        if (mappable.isNotEmpty()) {
            item(key = "map") {
                Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    HotelMarkersMap(mappable, onMarkerClick = onHotelClick)
                    val unmapped = content.hotels.size - mappable.size
                    Text(
                        text = if (unmapped > 0) "지도에 ${mappable.size}곳 표시 · 위치 정보가 없는 ${unmapped}곳은 목록에만 있어요"
                        else "지도에 ${mappable.size}곳 표시",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }

        items(content.hotels, key = { it.contentId }) { hotel ->
            HotelInfoCard(hotel = hotel, onClick = { onHotelClick(hotel) })
        }
    }
}

@Composable
private fun HotelMarkersMap(hotels: List<HotelInfoItem>, onMarkerClick: (HotelInfoItem) -> Unit) {
    val positions = hotels.map { LatLng(it.latitude!!, it.longitude!!) }
    val cameraPositionState = rememberCameraPositionState()
    var mapLoaded by remember { mutableStateOf(false) }

    // 결과가 바뀌면 모든 마커가 보이도록 카메라를 맞춘다(지도가 배치된 뒤에야 bounds 이동이 가능하다).
    LaunchedEffect(positions, mapLoaded) {
        if (!mapLoaded || positions.isEmpty()) return@LaunchedEffect
        val update = if (positions.size == 1) {
            CameraUpdateFactory.newLatLngZoom(positions.first(), 14f)
        } else {
            val bounds = LatLngBounds.builder().apply { positions.forEach(::include) }.build()
            CameraUpdateFactory.newLatLngBounds(bounds, 80)
        }
        cameraPositionState.move(update)
    }

    GoogleMap(
        modifier = Modifier
            .fillMaxWidth()
            .height(220.dp)
            .clip(RoundedCornerShape(12.dp)),
        cameraPositionState = cameraPositionState,
        onMapLoaded = { mapLoaded = true },
        uiSettings = MapUiSettings(zoomControlsEnabled = true, myLocationButtonEnabled = false)
    ) {
        hotels.zip(positions).forEach { (hotel, position) ->
            key(hotel.contentId) {
                Marker(
                    state = rememberUpdatedMarkerState(position = position),
                    title = hotel.name,
                    snippet = "눌러서 예약 사이트 보기",
                    onInfoWindowClick = { onMarkerClick(hotel) }
                )
            }
        }
    }
}

@Composable
private fun HotelInfoCard(hotel: HotelInfoItem, onClick: () -> Unit) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
    ) {
        Row(
            modifier = Modifier.padding(12.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(88.dp)
                    .clip(RoundedCornerShape(8.dp))
            ) {
                if (hotel.imageUrl != null) {
                    SubcomposeAsyncImage(
                        model = hotel.imageUrl,
                        contentDescription = hotel.name,
                        contentScale = ContentScale.Crop,
                        modifier = Modifier.fillMaxSize(),
                        loading = { PhotoPlaceholder(loading = true) },
                        error = { PhotoPlaceholder(loading = false) }
                    )
                } else {
                    PhotoPlaceholder(loading = false)
                }
            }

            Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Text(
                    text = hotel.name,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
                if (hotel.address.isNotBlank()) {
                    Text(
                        text = hotel.address,
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis
                    )
                }
                hotel.tel?.let {
                    Text(text = it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }
}

@Composable
private fun PhotoPlaceholder(loading: Boolean) {
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.surfaceVariant),
        contentAlignment = Alignment.Center
    ) {
        if (loading) {
            CircularProgressIndicator(modifier = Modifier.size(24.dp))
        } else {
            Icon(
                imageVector = Icons.Filled.Home,
                contentDescription = "사진 없음",
                tint = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

@Composable
private fun BookingSiteSheet(
    hotel: HotelInfoItem,
    onDismiss: () -> Unit,
    onOpenSite: (HotelBookingLinkBuilder.Site) -> Unit
) {
    ModalBottomSheet(onDismissRequest = onDismiss, sheetState = rememberModalBottomSheetState()) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 24.dp)
                .padding(bottom = 24.dp)
                .navigationBarsPadding(),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Text(hotel.name, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
            if (hotel.address.isNotBlank()) {
                Text(hotel.address, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Text(
                "가격과 예약 가능 여부는 예약 사이트에서 확인하세요. 숙소 이름으로 검색한 결과 페이지가 열려요.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Button(onClick = { onOpenSite(HotelBookingLinkBuilder.Site.AGODA) }, modifier = Modifier.fillMaxWidth()) {
                Text("아고다에서 보기")
            }
            OutlinedButton(onClick = { onOpenSite(HotelBookingLinkBuilder.Site.TRIP_COM) }, modifier = Modifier.fillMaxWidth()) {
                Text("트립닷컴에서 보기")
            }
        }
    }
}

@Composable
private fun CenteredMessage(message: String, actionLabel: String? = null, onAction: (() -> Unit)? = null) {
    Box(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        contentAlignment = Alignment.Center
    ) {
        Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text(message, style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
            if (actionLabel != null && onAction != null) {
                OutlinedButton(onClick = onAction) { Text(actionLabel) }
            }
        }
    }
}

// Custom Tabs로 연다. Custom Tabs를 지원하는 브라우저가 없으면 일반 브라우저 인텐트로, 그것도 없으면 안내만 한다.
private fun openInBrowser(context: Context, url: String) {
    val uri = Uri.parse(url)
    try {
        CustomTabsIntent.Builder().setShowTitle(true).build().launchUrl(context, uri)
    } catch (e: ActivityNotFoundException) {
        try {
            context.startActivity(Intent(Intent.ACTION_VIEW, uri).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        } catch (e: ActivityNotFoundException) {
            Toast.makeText(context, "웹 페이지를 열 수 있는 앱이 없어요.", Toast.LENGTH_SHORT).show()
        }
    }
}
