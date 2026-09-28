import java.util.Properties

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.ksp)
}
val localProperties = Properties()
localProperties.load(project.rootProject.file("local.properties").inputStream())
val mapsApiKey = localProperties.getProperty("google_maps_api_key")?:""

// FCM(푸시 알림)용 google-services.json이 이 모듈 바로 아래(app/google-services.json)에 있을 때만
// google-services 플러그인을 적용한다. 파일이 없으면(Firebase 프로젝트 설정 전) 이 플러그인 없이도
// 빌드는 정상적으로 되고, FCM 관련 기능만 런타임에 동작하지 않는다.
val hasGoogleServicesJson = file("google-services.json").exists()
if (hasGoogleServicesJson) {
    apply(plugin = "com.google.gms.google-services")
}

android {
    namespace = "com.example.testbuild01"
    compileSdk {
        version = release(36) {
            minorApiLevel = 1
        }
    }

    defaultConfig {
        applicationId = "com.example.testbuild01"
        minSdk = 24
        targetSdk = 36
        versionCode = 1
        versionName = "1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"

        buildConfigField("String", "google_maps_api_key", "\"$mapsApiKey\"")
        manifestPlaceholders["google_maps_api_key_"] = mapsApiKey
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }
    buildFeatures {
        compose = true
        buildConfig = true
    }
    // Room 마이그레이션 테스트(MigrationTestHelper)가 버전별 스키마 json을 읽을 수 있게 한다.
    sourceSets {
        getByName("androidTest").assets.srcDir("$projectDir/schemas")
    }
}

ksp {
    arg("room.schemaLocation", "$projectDir/schemas")
}

dependencies {
    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.navigation.compose)
    implementation(libs.maps.compose)
    implementation(libs.play.services.maps)
    implementation(libs.play.services.location)
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.graphics)
    implementation(libs.androidx.compose.ui.tooling.preview)
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.androidx.lifecycle.viewmodel.compose)
    implementation(libs.kotlinx.coroutines.android)
    implementation(libs.coil.compose)
    implementation("com.squareup.retrofit2:retrofit:2.9.0")
    implementation("com.squareup.retrofit2:converter-gson:2.9.0")
    implementation(libs.room.runtime)
    implementation(libs.room.ktx)
    ksp(libs.room.compiler)
    // lifecycle/navigation이 끌어오는 serialization-core 1.7.3을 1.8.1로 맞춘다. AGP는 androidTest 클래스패스를
    // 앱 버전에 strictly 고정하는데, room-testing(마이그레이션 테스트)의 스키마 파서는 1.8+ 기준으로 생성되어
    // 1.7.3과 섞이면 AbstractMethodError가 난다. 하위 호환되는 마이너 업데이트다.
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-core:1.8.1")
    implementation(libs.androidx.work.runtime.ktx)
    implementation(libs.androidx.camera.core)
    implementation(libs.androidx.camera.camera2)
    implementation(libs.androidx.camera.lifecycle)
    implementation(libs.androidx.camera.view)
    implementation(libs.mlkit.barcode.scanning)
    implementation(libs.tink.android)
    implementation(libs.zxing.core)
    // 호텔 예약 딥링크(Custom Tabs)
    implementation(libs.androidx.browser)
    // FCM(푸시 알림). google-services.json이 없어도 컴파일은 되며, 런타임 초기화만 실패한다
    // (TokenRegistrar/FcmService에서 예외를 잡아 앱이 죽지 않게 처리).
    implementation(platform("com.google.firebase:firebase-bom:34.19.0"))
    // 34.x BOM부터 -ktx 아티팩트가 폐기되고 코틀린 확장이 기본 아티팩트에 합쳐졌다.
    implementation("com.google.firebase:firebase-messaging")
    // 가계부: 영수증 OCR(ML Kit 한국어 텍스트 인식), 통계 차트(Vico), ViewModel (CameraX 는 위 선언 재사용)
    implementation("com.google.mlkit:text-recognition-korean:16.0.1")
    implementation("com.patrykandpatrick.vico:compose-m3:2.1.3")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.8.7")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.8.7")
    implementation("androidx.compose.material:material-icons-core")
    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.compose.ui.test.junit4)
    androidTestImplementation(libs.androidx.espresso.core)
    androidTestImplementation(libs.androidx.junit)
    androidTestImplementation(libs.room.testing)
    // API 36+ 기기에서 Espresso가 제거된 InputManager.getInstance()를 호출하지 않도록 1.7.x 계열로 맞춘다.
    androidTestImplementation("androidx.test:runner:1.7.0")
    androidTestImplementation("androidx.test:core:1.7.0")
    androidTestImplementation(libs.kotlinx.coroutines.test)
    debugImplementation(libs.androidx.compose.ui.test.manifest)
    debugImplementation(libs.androidx.compose.ui.tooling)

}

