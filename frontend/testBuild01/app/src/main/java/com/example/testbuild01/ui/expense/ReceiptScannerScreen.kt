@file:OptIn(ExperimentalMaterial3Api::class, ExperimentalGetImage::class)

package com.example.testbuild01.ui.expense

import android.Manifest
import android.content.pm.PackageManager
import android.util.Log
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.camera.core.CameraSelector
import androidx.camera.core.ExperimentalGetImage
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.LocalLifecycleOwner
import com.google.mlkit.vision.common.InputImage
import com.google.mlkit.vision.text.TextRecognition
import com.google.mlkit.vision.text.TextRecognizer
import com.google.mlkit.vision.text.korean.KoreanTextRecognizerOptions
import java.util.concurrent.Executors

// 영수증에서 금액만 인식한다. 인식 결과는 바로 저장하지 않고 항상 확인/수정 화면으로 넘긴다.
@Composable
fun ReceiptScannerScreen(
    viewModel: ExpenseViewModel,
    onBack: () -> Unit,
    onConfirm: () -> Unit
) {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val cameraExecutor = remember { Executors.newSingleThreadExecutor() }
    val textRecognizer = remember { TextRecognition.getClient(KoreanTextRecognizerOptions.Builder().build()) }
    var detectedAmount by remember { mutableStateOf<Long?>(null) }

    DisposableEffect(Unit) {
        onDispose {
            textRecognizer.close()
            cameraExecutor.shutdown()
        }
    }

    var hasCameraPermission by remember {
        mutableStateOf(
            ContextCompat.checkSelfPermission(context, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED
        )
    }
    val launcher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        hasCameraPermission = granted
    }
    LaunchedEffect(Unit) {
        if (!hasCameraPermission) launcher.launch(Manifest.permission.CAMERA)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("영수증 스캔") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "뒤로 가기")
                    }
                }
            )
        }
    ) { innerPadding ->
        if (hasCameraPermission) {
            Column(modifier = Modifier.fillMaxSize().padding(innerPadding)) {
                Box(modifier = Modifier.weight(0.7f)) {
                    AndroidView(
                        factory = { ctx ->
                            val previewView = PreviewView(ctx)
                            val providerFuture = ProcessCameraProvider.getInstance(ctx)
                            providerFuture.addListener({
                                val cameraProvider = providerFuture.get()
                                val preview = Preview.Builder().build()
                                    .also { it.setSurfaceProvider(previewView.surfaceProvider) }
                                val analysis = ImageAnalysis.Builder()
                                    .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                                    .build().also {
                                        it.setAnalyzer(cameraExecutor) { imageProxy ->
                                            processImage(imageProxy, textRecognizer) { text ->
                                                extractPrice(text)?.let { price -> detectedAmount = price }
                                            }
                                        }
                                    }
                                try {
                                    cameraProvider.unbindAll()
                                    cameraProvider.bindToLifecycle(
                                        lifecycleOwner, CameraSelector.DEFAULT_BACK_CAMERA, preview, analysis
                                    )
                                } catch (e: Exception) {
                                    Log.e("OCR", "Camera failed", e)
                                }
                            }, ContextCompat.getMainExecutor(ctx))
                            previewView
                        },
                        modifier = Modifier.fillMaxSize()
                    )
                }

                Card(modifier = Modifier.fillMaxWidth().weight(0.3f).padding(16.dp)) {
                    Column(
                        modifier = Modifier.padding(16.dp).fillMaxSize(),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text(
                            detectedAmount?.let { "인식된 금액: ${"%,d".format(it)}원" } ?: "영수증을 비춰주세요",
                            style = MaterialTheme.typography.titleLarge
                        )
                        Button(
                            onClick = {
                                viewModel.updateScannedAmount(detectedAmount)
                                onConfirm()
                            },
                            enabled = detectedAmount != null,
                            modifier = Modifier.fillMaxWidth()
                        ) { Text("확인/수정하기") }
                    }
                }
            }
        } else {
            Box(
                modifier = Modifier.fillMaxSize().padding(innerPadding),
                contentAlignment = Alignment.Center
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text("영수증 인식을 위해 카메라 권한이 필요합니다.")
                    Spacer(Modifier.height(8.dp))
                    Button(onClick = { launcher.launch(Manifest.permission.CAMERA) }) { Text("권한 다시 요청하기") }
                    TextButton(onClick = {
                        viewModel.updateScannedAmount(null)
                        onConfirm()
                    }) { Text("직접 입력하기") }
                }
            }
        }
    }
}

private fun processImage(proxy: ImageProxy, recognizer: TextRecognizer, onSuccess: (String) -> Unit) {
    val mediaImage = proxy.image
    if (mediaImage == null) {
        proxy.close()
        return
    }
    val image = InputImage.fromMediaImage(mediaImage, proxy.imageInfo.rotationDegrees)
    recognizer.process(image)
        .addOnSuccessListener { res -> onSuccess(res.text) }
        .addOnCompleteListener { proxy.close() }
}

// 가장 큰 숫자를 총액으로 본다. 어디까지나 추정값이라 사용자가 확인 화면에서 수정한다.
internal fun extractPrice(text: String): Long? {
    val regex = Regex("""\d{1,3}(,\d{3})+|\d{4,}""")
    return regex.findAll(text)
        .mapNotNull { it.value.replace(",", "").toLongOrNull() }
        .filter { it > 100 }
        .maxOrNull()
}
