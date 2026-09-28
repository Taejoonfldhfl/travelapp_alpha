// Top-level build file where you can add configuration options common to all sub-projects/modules.
plugins {
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.kotlin.compose) apply false
    alias(libs.plugins.ksp) apply false
    // FCM(google-services.json) 연동용. app 모듈에서 파일이 있을 때만 실제로 적용한다(app/build.gradle.kts 참고).
    id("com.google.gms.google-services") version "4.5.0" apply false
}