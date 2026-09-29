package com.example.testbuild01.ui.ticket

import android.Manifest
import android.content.pm.PackageManager
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.camera.core.CameraSelector
import androidx.camera.core.ExperimentalGetImage
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.lifecycle.LifecycleOwner
import com.example.testbuild01.data.local.ScanRecord
import com.example.testbuild01.data.local.ScanResultStore
import com.example.testbuild01.data.local.toHex
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.common.InputImage
import kotlinx.coroutines.delay
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicReference

private const val SCAN_HINT_DELAY_MS = 10_000L

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TicketScanScreen(
    onScanned: (ScanOutcome.Success) -> Unit,
    onOcrScan: () -> Unit,
    onManualEntry: () -> Unit,
    onBack: () -> Unit
) {
    val context = LocalContext.current

    var hasPermission by remember {
        mutableStateOf(
            ContextCompat.checkSelfPermission(context, Manifest.permission.CAMERA) ==
                PackageManager.PERMISSION_GRANTED
        )
    }
    var permissionDenied by remember { mutableStateOf(false) }
    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        hasPermission = granted
        permissionDenied = !granted
    }
    LaunchedEffect(Unit) {
        if (!hasPermission) permissionLauncher.launch(Manifest.permission.CAMERA)
    }

    var message by remember { mutableStateOf<String?>(null) }
    var showHint by remember { mutableStateOf(false) }
    LaunchedEffect(hasPermission) {
        if (hasPermission) {
            delay(SCAN_HINT_DELAY_MS)
            showHint = true
        }
    }

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text("티켓 스캔") },
                navigationIcon = { TextButton(onClick = onBack) { Text("뒤로") } }
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            if (hasPermission) {
                CameraPreview(
                    modifier = Modifier
                        .fillMaxWidth()
                        .weight(1f),
                    onOutcome = { outcome ->
                        when (outcome) {
                            is ScanOutcome.Success -> onScanned(outcome)
                            is ScanOutcome.Failure ->
                                message = "바코드를 감지했지만 값을 읽지 못했습니다. 다시 비추거나 직접 입력해 주세요."
                        }
                    },
                    onError = { message = "스캔 중 오류가 발생했습니다. 직접 입력으로 등록할 수 있습니다." }
                )
            } else {
                Text(
                    text = if (permissionDenied) {
                        "카메라 권한이 거부되어 스캔할 수 없습니다. 직접 입력으로 등록해 주세요."
                    } else {
                        "카메라 권한을 요청하는 중입니다."
                    },
                    modifier = Modifier
                        .weight(1f)
                        .padding(16.dp)
                )
            }

            val hint = message
                ?: if (showHint) "스캔이 잘 되지 않나요? 직접 입력으로 등록할 수 있습니다." else null
            if (hint != null) {
                Text(
                    text = hint,
                    style = MaterialTheme.typography.bodyMedium,
                    modifier = Modifier.padding(horizontal = 16.dp)
                )
            }

            TextButton(
                onClick = onOcrScan,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(start = 16.dp, end = 16.dp)
            ) {
                Text("바코드가 안 읽히나요? 문자 인식(OCR)으로 스캔")
            }

            Button(
                onClick = onManualEntry,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(start = 16.dp, end = 16.dp, bottom = 16.dp)
            ) {
                Text("직접 입력")
            }
        }
    }
}

@androidx.annotation.OptIn(ExperimentalGetImage::class)
@Composable
private fun CameraPreview(
    modifier: Modifier,
    onOutcome: (ScanOutcome) -> Unit,
    onError: (Throwable) -> Unit
) {
    val context = LocalContext.current
    val lifecycleOwner = context as LifecycleOwner
    val currentOnOutcome by rememberUpdatedState(onOutcome)
    val currentOnError by rememberUpdatedState(onError)

    val executor = remember { Executors.newSingleThreadExecutor() }
    val scanner = remember { BarcodeScanning.getClient() }
    // 성공 결과를 한 번만 전달하기 위한 플래그
    val delivered = remember { AtomicBoolean(false) }
    // 진단용 스캔 기록. 같은 프레임 결과가 반복 기록되지 않도록 마지막 값을 기억한다.
    val store = remember { ScanResultStore(context) }
    val lastRecordKey = remember { AtomicReference<String?>(null) }
    val reportError: (Throwable) -> Unit = { error ->
        store.append(
            ScanRecord(
                timestamp = System.currentTimeMillis(),
                outcome = "ERROR",
                format = "",
                valueType = null,
                rawValue = null,
                displayValue = null,
                rawBytesHex = null,
                message = error.toString()
            )
        )
        currentOnError(error)
    }
    var cameraProvider by remember { mutableStateOf<ProcessCameraProvider?>(null) }

    DisposableEffect(Unit) {
        onDispose {
            cameraProvider?.unbindAll()
            scanner.close()
            executor.shutdown()
        }
    }

    AndroidView(
        modifier = modifier,
        factory = { ctx ->
            val previewView = PreviewView(ctx)
            val providerFuture = ProcessCameraProvider.getInstance(ctx)
            providerFuture.addListener({
                val provider = try {
                    providerFuture.get()
                } catch (e: Exception) {
                    reportError(e)
                    return@addListener
                }
                cameraProvider = provider

                val preview = Preview.Builder().build().also {
                    it.surfaceProvider = previewView.surfaceProvider
                }
                val analysis = ImageAnalysis.Builder()
                    .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                    .build()
                analysis.setAnalyzer(executor) { proxy ->
                    val mediaImage = proxy.image
                    if (mediaImage == null || delivered.get()) {
                        proxy.close()
                        return@setAnalyzer
                    }
                    val image = InputImage.fromMediaImage(mediaImage, proxy.imageInfo.rotationDegrees)
                    scanner.process(image)
                        .addOnSuccessListener { barcodes ->
                            val barcode = barcodes.firstOrNull() ?: return@addOnSuccessListener
                            val outcome = BarcodeParser.parse(barcode.rawValue, barcode.format)
                            val key = "${barcode.format}:${barcode.rawValue}"
                            if (lastRecordKey.getAndSet(key) != key) {
                                store.append(
                                    ScanRecord(
                                        timestamp = System.currentTimeMillis(),
                                        outcome = if (outcome is ScanOutcome.Success) "SUCCESS" else "EMPTY",
                                        format = BarcodeParser.formatName(barcode.format),
                                        valueType = barcode.valueType,
                                        rawValue = barcode.rawValue,
                                        displayValue = barcode.displayValue,
                                        rawBytesHex = barcode.rawBytes?.toHex()
                                    )
                                )
                            }
                            when (outcome) {
                                is ScanOutcome.Success ->
                                    if (delivered.compareAndSet(false, true)) currentOnOutcome(outcome)
                                is ScanOutcome.Failure -> currentOnOutcome(outcome)
                            }
                        }
                        .addOnFailureListener { reportError(it) }
                        .addOnCompleteListener { proxy.close() }
                }

                try {
                    provider.unbindAll()
                    provider.bindToLifecycle(
                        lifecycleOwner,
                        CameraSelector.DEFAULT_BACK_CAMERA,
                        preview,
                        analysis
                    )
                } catch (e: Exception) {
                    reportError(e)
                }
            }, ContextCompat.getMainExecutor(ctx))
            previewView
        }
    )
}
