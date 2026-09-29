# TravelApp Android 클라이언트

TravelApp의 Android 앱입니다(Kotlin, Jetpack Compose). 전체 프로젝트 개요는 [저장소 루트 README](../../README.md), 백엔드 API 명세는 [`backend/README.md`](../../backend/README.md)를 참고하세요.

## 화면 구성

`MainActivity`의 단일 `NavHost`에 모든 화면이 등록되어 있습니다. 주요 흐름:

- **로그인/회원가입** → 자동 로그인(저장된 JWT가 만료 전이면 바로 건너뜀) → **여행 프로젝트 선택**(로그아웃 버튼 포함)
- **지도 화면**(여행별 진입점) → 일정 · 멤버 · AI 챗봇 장소추천 · 가계부 · 숙소 · 티켓으로 분기
- **일정**: 등록/수정/삭제, 경로 최적화 결과 반영
- **AI 챗봇**: 대화창 + 추천 카드("일정에 추가" 버튼 포함)
- **숙소**: 검색(TourAPI) → 확인서 OCR 스캔 또는 수동 입력 → 검토/저장 → 일정 자동 연동, 예약 딥링크(아고다/트립닷컴)
- **티켓**: 바코드/QR 스캔 또는 문자 인식(OCR) 대안 스캔 또는 수동 입력 → 검토/저장, 알림 스케줄링
- **가계부**: 지출 등록/수정/삭제(영수증 OCR 스캔 지원), 통계, 예산, 더치페이 정산 및 확정/공유

## 아키텍처 개요

- **UI**: Jetpack Compose. 화면마다 Composable + (필요시) ViewModel. `TicketViewModel`/`ExpenseViewModel`은 여러 화면이 상태를 공유해야 해서 Activity 범위로 생성됩니다.
- **네트워크**: Retrofit(`TravelApiService`) + `RetrofitClient`의 OkHttp 인터셉터가 저장된 JWT를 자동으로 실어 보내고, 401 응답(로그인/회원가입 제외)을 받으면 토큰을 지우고 `AuthEvents`로 세션 만료를 알립니다.
- **인증**: `TokenManager` + `JwtExpiry`(Android API 버전에 의존하지 않는 순수 Kotlin Base64URL 디코더)로 토큰 만료를 직접 판단해 자동 로그인 여부를 결정합니다. `AuthSession.logout()`이 서버의 FCM 토큰 삭제와 로컬 토큰 삭제 순서를 보장합니다.
- **로컬 저장소**: 티켓 상세 정보는 Room(`TicketDatabase`)에 저장하되 민감한 필드는 Tink로 암호화(`TicketCipher`)합니다.
- **OCR/스캔**: ML Kit 텍스트 인식(한국어)으로 호텔 확인서/영수증/티켓(대안 경로)을 스캔하는 공용 뼈대(`OcrScannerScaffold`)를 재사용하고, 티켓 바코드/QR은 ML Kit 바코드 인식으로 별도 처리합니다. 인식 결과는 항상 확인/수정 화면을 거친 뒤에만 저장됩니다.
- **알림**: FCM(서버발: 예산 초과, 정산 결과 도착 등) + 로컬 알람(`AlarmManager`, 티켓 출발 전/무료취소 마감 알림).

## 패키지 구조

```
app/src/main/java/com/example/testbuild01/
  data/
    local/         # TokenManager, JwtExpiry, Room(Ticket*), 암호화, 로컬 모델
    model/         # 서버 DTO에 대응하는 Kotlin 데이터 클래스
    network/       # Retrofit 서비스, RetrofitClient, AuthEvents/AuthSession
    repository/    # 화면-서버/로컬 저장소 사이의 도메인 로직
    hotel/, ticket/ # 호텔·티켓 OCR 추출기 등 순수 로직
  notification/    # FCM 수신, 디바이스 토큰 등록, 로컬 알람 스케줄링
  ui/
    aichat/, hotel/, ticket/, expense/, common/ocr/  # 화면별 Composable + ViewModel
  MainActivity.kt  # NavHost, 화면 간 라우팅
  MainApplication.kt # 앱 전역 싱글톤 초기화(Repository, FCM 등)
```

## 기술 스택

Jetpack Compose · Navigation Compose · Retrofit + Gson · Room · WorkManager · CameraX · ML Kit(텍스트 인식 · 바코드 인식) · Tink(암호화) · Firebase Cloud Messaging · Google Maps/Places Compose · Coil · Vico(가계부 통계 차트)

## 실행 전 준비

1. **`local.properties`**에 `google_maps_api_key`를 등록해야 하며, 해당 키에 **Maps SDK for Android**와 **Places API**가 모두 활성화되어 있어야 합니다.
   ```properties
   google_maps_api_key=발급받은_키
   ```
2. **백엔드 서버 주소**: `data/network/RetrofitClient.kt`의 `BASE_URL`이 에뮬레이터 기준 로컬 백엔드(`http://10.0.2.2:5020/`)로 하드코딩되어 있습니다. 실제 서버 주소/포트에 맞게 직접 수정해야 합니다(시연용 개발 단계라 별도 설정 파일로 분리하지 않았습니다).
3. **FCM(선택)**: `app/google-services.json`이 있어야 실제 푸시 알림이 동작합니다. 없어도 빌드/실행은 정상이며 FCM 관련 호출만 조용히 건너뜁니다.
4. 백엔드가 먼저 떠 있어야 합니다 — [`backend/README.md`](../../backend/README.md)의 "0. 실행 전 준비" 참고.

## 테스트

```
./gradlew testDebugUnitTest
```

OCR 필드 추출(호텔/티켓), 바코드 파싱, JWT 만료 판단, 정산 결과 포맷, 호텔↔일정 연동 매핑, 예약 딥링크 생성 등 순수 로직 위주로 JVM 유닛테스트가 갖춰져 있습니다.

`app/src/androidTest`에 Room 마이그레이션 테스트와 호텔 관련 UI/E2E 테스트도 있지만, 에뮬레이터/실기기가 필요해 `./gradlew connectedAndroidTest`로 별도 실행해야 합니다.

## 알려진 제한사항

- `BASE_URL`이 코드에 하드코딩되어 있어 배포 환경마다 코드를 고쳐 다시 빌드해야 합니다.
- 야놀자/여기어때 예약 딥링크는 아직 없습니다(정확한 검색 URL 패턴 미확인).
