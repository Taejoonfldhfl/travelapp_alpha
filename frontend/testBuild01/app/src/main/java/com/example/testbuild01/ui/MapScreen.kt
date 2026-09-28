package com.example.testbuild01.ui

import android.Manifest
import android.annotation.SuppressLint
import android.content.pm.PackageManager
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import com.google.android.gms.location.LocationServices
import com.google.android.gms.maps.model.CameraPosition
import com.google.android.gms.maps.model.LatLng
import com.google.maps.android.compose.*

@SuppressLint("MissingPermission")
@Composable
fun MapScreen(
    tripId: Int,
    projectName: String,
    onScheduleSelected: () -> Unit,
    onMemberSelected: () -> Unit,
    onPlaceRecommendationSelected: () -> Unit = {},
    onTicketScanSelected: () -> Unit = {},
    onTicketListSelected: () -> Unit = {},
    onExpenseSelected: () -> Unit = {},
    onHotelListSelected: () -> Unit = {}) {
    val context = LocalContext.current
    val fusedLocationClient = remember { LocationServices.getFusedLocationProviderClient(context) }
    
    val initialLocation = LatLng(37.5665, 126.9780) // Default Seoul
    val cameraPositionState = rememberCameraPositionState {
        position = CameraPosition.fromLatLngZoom(initialLocation, 14f)
    }

    var hasLocationPermission by remember {
        mutableStateOf(
            ContextCompat.checkSelfPermission(
                context,
                Manifest.permission.ACCESS_FINE_LOCATION
            ) == PackageManager.PERMISSION_GRANTED
        )
    }

    val launcher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestMultiplePermissions()
    ) { permissions ->
        hasLocationPermission = permissions.getOrDefault(Manifest.permission.ACCESS_FINE_LOCATION, false) ||
                                permissions.getOrDefault(Manifest.permission.ACCESS_COARSE_LOCATION, false)
    }

    LaunchedEffect(hasLocationPermission) {
        if (!hasLocationPermission) {
            launcher.launch(
                arrayOf(
                    Manifest.permission.ACCESS_FINE_LOCATION,
                    Manifest.permission.ACCESS_COARSE_LOCATION
                )
            )
        } else {
            fusedLocationClient.lastLocation.addOnSuccessListener { location ->
                location?.let {
                    cameraPositionState.position = CameraPosition.fromLatLngZoom(
                        LatLng(it.latitude, it.longitude),
                        14f
                    )
                }
            }
        }
    }

    Box(modifier = Modifier.fillMaxSize()) {
        GoogleMap(
            modifier = Modifier
                .fillMaxWidth()
                .fillMaxHeight(0.9f)
                .align(Alignment.TopCenter),
            contentPadding = PaddingValues(top = 50.dp, end = 20.dp),
            cameraPositionState = cameraPositionState,
            properties = MapProperties(
                isMyLocationEnabled = hasLocationPermission,
                mapType = MapType.NORMAL
            ),
            uiSettings = MapUiSettings(myLocationButtonEnabled = true)
        )

        // Overlay with project name
        Surface(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .padding(top = 50.dp),
            color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.8f),
            shape = MaterialTheme.shapes.medium
        ) {
            Text(
                text = projectName,
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp),
                style = MaterialTheme.typography.titleMedium
            )
        }

        // 티켓 목록 바로가기
        FilledTonalButton(
            onClick = onTicketListSelected,
            modifier = Modifier
                .align(Alignment.TopStart)
                .padding(top = 50.dp, start = 16.dp)
        ) {
            Text("내 티켓")
        }

        // 숙소 정보 찾기(TourAPI) 바로가기
        FilledTonalButton(
            onClick = onHotelListSelected,
            modifier = Modifier
                .align(Alignment.TopEnd)
                .padding(top = 50.dp, end = 16.dp)
        ) {
            Text("숙소 찾기")
        }

        // Bottom Bar with 4 buttons
        Surface(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .fillMaxHeight(0.1f),

            color = MaterialTheme.colorScheme.surface,
            tonalElevation = 4.dp
        ) {
            Row(
                modifier = Modifier
                    .padding(16.dp)
                    .fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceEvenly
            ) {
                Button(onClick = onScheduleSelected) { Text("일정") }
                Button(onClick = onExpenseSelected) { Text("가계부") }
                Button(onClick = onPlaceRecommendationSelected) { Text("장소추천") }
                Button(onClick = onTicketScanSelected) { Text("티켓 스캔") }
                Button(onClick = onMemberSelected) { Text("멤버") }
            }
        }
    }
}

@Preview(showBackground = true)
@Composable
fun MapScreenPreview() {
    MapScreen(tripId = 1,
        projectName = "서울 여행 (Preview)",
        onScheduleSelected = {},
        onMemberSelected = {})
}
