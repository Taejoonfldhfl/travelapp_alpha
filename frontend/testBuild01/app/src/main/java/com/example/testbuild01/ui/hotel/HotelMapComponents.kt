package com.example.testbuild01.ui.hotel

import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.SemanticsPropertyKey
import androidx.compose.ui.semantics.SemanticsPropertyReceiver
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.example.testbuild01.data.local.HotelDetail
import com.google.android.gms.maps.model.CameraPosition
import com.google.android.gms.maps.model.LatLng
import com.google.maps.android.compose.GoogleMap
import com.google.maps.android.compose.MapUiSettings
import com.google.maps.android.compose.Marker
import com.google.maps.android.compose.rememberCameraPositionState
import com.google.maps.android.compose.rememberMarkerState

const val HOTEL_MAP_TEST_TAG = "hotel_location_map"

/**
 * 지도 SDK 내부(카메라·마커 객체)는 Compose 시맨틱스에 드러나지 않으므로, 실제 상태 객체에서 읽은 값을 노출해
 * UI 테스트가 초기 카메라 위치·마커 제목·지도 로드 여부를 확인할 수 있게 한다.
 */
val MapCameraTarget = SemanticsPropertyKey<LatLng>("MapCameraTarget")
var SemanticsPropertyReceiver.mapCameraTarget by MapCameraTarget
val MapMarkerPosition = SemanticsPropertyKey<LatLng>("MapMarkerPosition")
var SemanticsPropertyReceiver.mapMarkerPosition by MapMarkerPosition
val MapMarkerTitle = SemanticsPropertyKey<String>("MapMarkerTitle")
var SemanticsPropertyReceiver.mapMarkerTitle by MapMarkerTitle
val MapLoaded = SemanticsPropertyKey<Boolean>("MapLoaded")
var SemanticsPropertyReceiver.mapLoaded by MapLoaded

/** 위/경도가 있으면 작은 미니맵 + 길찾기 버튼을, 없으면 주소 텍스트 + 외부 지도 앱 Intent로 대체한다. */
@Composable
fun HotelLocationSection(hotel: HotelDetail) {
    val context = LocalContext.current
    val lat = hotel.latitude
    val lng = hotel.longitude

    Column {
        if (lat != null && lng != null) {
            val position = LatLng(lat, lng)
            val cameraPositionState = rememberCameraPositionState(key = "$lat,$lng") {
                this.position = CameraPosition.fromLatLngZoom(position, 15f)
            }
            // 카메라와 같은 키를 써야 호텔 좌표를 수정했을 때 마커도 새 위치로 옮겨진다.
            val markerState = rememberMarkerState(key = "$lat,$lng", position = position)
            val markerTitle = hotel.hotelName
            var loaded by remember { mutableStateOf(false) }
            // 컴포지션에서 읽어 두어야 지도가 카메라를 움직였을 때 시맨틱스도 다시 계산된다.
            val cameraTarget = cameraPositionState.position.target
            GoogleMap(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(120.dp)
                    .testTag(HOTEL_MAP_TEST_TAG)
                    .semantics {
                        mapCameraTarget = cameraTarget
                        mapMarkerPosition = markerState.position
                        mapMarkerTitle = markerTitle
                        mapLoaded = loaded
                    },
                cameraPositionState = cameraPositionState,
                onMapLoaded = { loaded = true },
                uiSettings = MapUiSettings(
                    zoomControlsEnabled = false,
                    scrollGesturesEnabled = false,
                    zoomGesturesEnabled = false,
                    myLocationButtonEnabled = false
                )
            ) {
                Marker(state = markerState, title = markerTitle)
            }
        } else {
            Text(hotel.address, style = MaterialTheme.typography.bodyMedium)
        }

        TextButton(onClick = { openExternalMap(context, hotel) }) { Text("길찾기") }
    }
}

private fun openExternalMap(context: Context, hotel: HotelDetail) {
    // 특정 앱을 지정하지 않은 암시적 geo: 인텐트를 써서, API 30+ 패키지 가시성 제약(<queries>) 없이 동작하게 한다.
    val uri = if (hotel.latitude != null && hotel.longitude != null) {
        Uri.parse("geo:${hotel.latitude},${hotel.longitude}?q=${hotel.latitude},${hotel.longitude}(${Uri.encode(hotel.hotelName)})")
    } else {
        Uri.parse("geo:0,0?q=" + Uri.encode(hotel.address))
    }
    context.startActivity(Intent(Intent.ACTION_VIEW, uri))
}
