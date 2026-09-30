# TravelApp

여행 일정·경로·숙소·티켓·가계부를 한 앱에서 관리하고, AI 챗봇이 대화로 근처 장소를 추천해주는 여행 관리 앱입니다. Android(Kotlin/Compose) 클라이언트와 ASP.NET Core(.NET 10) 백엔드로 구성되어 있으며, 현재 시연용 개발 단계입니다.

## 주요 기능

### 여행 · 일정
- 여행 생성/멤버 초대, 일정 CRUD
- 같은 여행 일정끼리 시간대 겹침 검증
- **경로 최적화**: Held-Karp(정확해, N≤10) → OR-Tools(짧은 시간예산) → 유전 알고리즘 순 하이브리드 TSP. 호텔 체크인처럼 "이 시각 이전엔 방문 불가"한 앵커 제약을 후처리로 반영
- **Stage 2 — 우선순위 기반 시간예산 스킵**: 하루 일정이 배정 가능한 시간을 넘을 것으로 예상되면 우선순위 낮은(비필수) 일정부터 자동 제외하고 재계산. 호텔 체크인 등 앵커는 우선순위와 무관하게 항상 포함. 전부 필수라 뺄 게 없으면 스킵 없이 "시간 초과" 경고만 반환

### AI 챗봇 장소추천
- 지도 화면의 "장소추천" 버튼으로 진입, 대화로 받은 취향에 맞는 장소를 1~3개 추천
- 추천은 반드시 실제 검색 API(Tmap) 후보 목록 안에서만 고르도록 서버가 검증(그라운딩) — 위치를 특정 못 하거나 후보가 없으면 LLM을 부르지 않고 이유를 바로 답해 환각을 방지
- 추천 카드에는 서버가 검증한 대표 사진(`IPlaceImageProvider`, 실패 시 Google Places 폴백 → 플레이스홀더)이 함께 표시되고, "일정에 추가"로 바로 저장 가능

### 숙소(호텔)
- 한국관광공사 TourAPI로 키워드/지역/현재위치 기반 숙박시설 검색
- 호텔 확인서 사진을 OCR로 스캔해 체크인/체크아웃/예약번호 등을 자동 인식(확인/수정 화면에서 검토 후 저장)
- 예약을 여행 일정에 자동 연동(체크인/체크아웃을 경로 최적화 앵커로 사용)
- 아고다 · 트립닷컴 예약 페이지로 바로 이동하는 딥링크(제휴 링크 아님, 일반 검색 페이지 연결)

### 티켓(항공권 · 버스표)
- 바코드/QR 스캔으로 등록(카메라 미리보기 + ML Kit 바코드 인식)
- 바코드가 없거나 인식이 안 될 때를 위한 문자 인식(OCR) 대안 경로 — 편명 패턴이 있으면 항공, "고속버스" 등 키워드가 있으면 버스로 자동 판별
- 상세 정보는 로컬에 암호화 저장(Tink), 출발 전/체크아웃 무료취소 마감 알림

### 가계부(지출 관리)
- 지출 등록/수정/삭제, 더치페이 분담(균등/커스텀 금액), 카테고리별 지출 비율
- 예산 설정 및 "예산 이내 → 초과"로 바뀌는 순간에만 알림(이미 초과 상태에서 추가 지출해도 중복 알림 없음)
- 더치페이 정산: 최소 송금 횟수로 계산, 확정(finalize) 시 스냅샷으로 저장해 이후 지출이 바뀌어도 이미 공유한 정산 내용은 유지. 지출이 그대로면 재확정해도 새 스냅샷/알림 없이 기존 확정본 반환(멱등)

### 로그인 · 인증
- 회원가입/로그인, JWT 기반 인증
- 자동 로그인: 저장된 토큰이 있고 만료되지 않았을 때만 로그인 상태로 판단(순수 Kotlin으로 구현한 JWT exp 파서, Android API 버전에 의존하지 않음)
- 인증이 필요한 요청이 401을 받으면 자동으로 로그아웃 처리 후 안내와 함께 로그인 화면으로 이동
- 로그아웃 시 서버에 등록된 FCM 디바이스 토큰을 먼저 정리
- 같은 기기에서 계정을 바꿔 로그인해도 FCM 토큰이 이전 계정에 남지 않도록 자동 이전

### 알림(FCM)
- 예산 초과, 정산 결과 도착, 티켓 알림 등을 서버에서 실제 FCM 푸시로 발송(계정당 디바이스 토큰 1개 유지)

## 기술 스택

**백엔드** — ASP.NET Core(.NET 10), Entity Framework Core + PostgreSQL, JWT 인증, Google OR-Tools(경로 최적화), Anthropic Claude API(장소추천), Firebase Admin SDK(FCM), 한국관광공사 TourAPI, Tmap(SK Open API, 경로/거리)

**프론트엔드** — Android(Kotlin, Jetpack Compose), Retrofit, Room, ML Kit(텍스트 인식·바코드 인식), CameraX, Firebase Cloud Messaging, Google Maps/Places SDK

## 프로젝트 구조

```
backend/
  TravelApp.WebAPI/        # API 서버 본체
  TravelApp.WebAPI.Tests/  # 백엔드 유닛/컨트롤러 테스트
  TravelApp.Expense.Tests/ # 가계부 도메인 테스트
  SharedData/              # DTO/모델 (WebAPI와 테스트 프로젝트가 공유)
frontend/testBuild01/      # Android 앱 (Kotlin, Jetpack Compose)
```

## 실행 전 준비

**백엔드**
```
cd backend/TravelApp.WebAPI
dotnet user-secrets set "Anthropic:ApiKey" "발급받은_키"
dotnet user-secrets set "Tmap:AppKey" "발급받은_키"
dotnet user-secrets set "Jwt:Key" "충분히_긴_임의_문자열"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=TravelDb;Username=postgres;Password=..."
dotnet ef database update
```

`appsettings.json`에는 플레이스홀더만 있고 실제 값은 커밋하지 않습니다. 로컬 개발은 `dotnet user-secrets`로, 운영 배포는 같은 이름의 환경변수(`Jwt__Key` 등)로 주입합니다. 시크릿이 플레이스홀더로 남아 있으면 시작 시 경고 로그가 남고, `Anthropic:ApiKey`가 없으면 앱이 기동 즉시 종료됩니다(의도된 fail-fast).

`PlaceStatus:Provider`를 `Google`로 설정한 경우에만 아래 키가 필요합니다(기본값 `Mock`에서는 필요 없음):
```
dotnet user-secrets set "Google:PlacesApiKey" "발급받은_키"
```

**프론트엔드**

`local.properties`에 `google_maps_api_key`가 등록되어 있어야 하며, 해당 키에 **Maps SDK for Android**와 **Places API**가 모두 활성화되어 있어야 합니다. FCM을 쓰려면 `app/google-services.json`이 필요하지만, 없어도 나머지 기능은 정상 동작합니다(FCM 관련 호출만 조용히 건너뜀).

## 테스트

```
cd backend && dotnet test TravelApp.WebAPI.slnx
cd frontend/testBuild01 && ./gradlew testDebugUnitTest
```

백엔드는 컨트롤러별 유닛테스트(EF Core InMemory DB 사용), 프론트엔드는 OCR 추출·바코드 파싱·정산 계산 등 순수 로직 위주로 JVM 유닛테스트를 갖추고 있습니다.

## 알려진 제한사항

- Tmap POI 응답에는 휴관/임시휴업 여부 필드가 없습니다(항상 Unknown). `PlaceStatus:Provider`를 `Google`로 설정하면 영업상태를 모르는 장소만 Google Places API(New)의 `businessStatus`(OPERATIONAL/CLOSED_TEMPORARILY/CLOSED_PERMANENTLY)로 보완합니다. 기본값(`Mock`)에서는 이 보완이 켜지지 않고 기존 동작(Unknown 유지)이 그대로 유지됩니다.
- Tmap의 배치 경로 매트릭스 API(`/tmap/matrix`)는 존재가 확인됐지만, 실시간 정체 반영 여부가 불확실해 현재 경로 최적화는 지점 쌍마다 개별 호출하는 방식을 유지하고 있습니다.
- 야놀자/여기어때 예약 딥링크는 정확한 검색 URL 패턴을 확인하지 못해 아직 추가하지 않았습니다.

## 더 자세한 내용

전체 API 명세는 [`backend/README.md`](backend/README.md)를 참고하세요.
