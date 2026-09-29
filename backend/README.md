# TravelApp API 명세서

`User / Trip / TripMember / Schedule / RouteOptimization / AiChat / HotelInfo / Expense` API 기준 명세서입니다.

---

# 0. 실행 전 준비

`appsettings.json`에는 실제 키/비밀번호 대신 플레이스홀더만 있습니다. 실제 값은 파일을 고치지 말고 **로컬 개발은 `dotnet user-secrets`**, **운영 배포는 같은 이름의 환경변수**(`:`를 `__`로 바꿔서, 예: `Jwt__Key`)로 주입합니다.

```bash
cd backend/TravelApp.WebAPI

# DB 연결 문자열 (PostgreSQL)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=TravelDb;Username=postgres;Password=<DB_비밀번호>"

# JWT 서명 키 (32자 이상 임의 문자열)
dotnet user-secrets set "Jwt:Key" "<충분히_긴_임의_문자열>"

# Anthropic API 키 (AiChat 장소추천에 필요 — 없으면 앱이 기동 시점에 즉시 종료됨)
dotnet user-secrets set "Anthropic:ApiKey" "<발급받은_키>"

# Tmap(SK Open API) 앱키 — RouteOptimization/PlaceSearch 기본 Provider가 Tmap이라 사실상 필수
dotnet user-secrets set "Tmap:AppKey" "<발급받은_키>"

# DB 스키마 적용
dotnet ef database update
```

| 시크릿 키 | 필요 조건 | 없을 때 |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | 항상 | DB 연결 실패로 대부분의 API 호출이 실패 |
| `Jwt:Key` | 항상 | 앱은 뜨지만 로그인 시 발급된 토큰이 안전하지 않음(시작 시 경고 로그) |
| `Anthropic:ApiKey` | 항상(AiChat 기능 자체가 필수 의존) | **앱이 기동 즉시 종료**(`AnthropicStartupValidator`가 fail-fast 처리) |
| `Tmap:AppKey` | `RouteOptimization:Provider`/`PlaceSearch:Provider`가 기본값 `Tmap`일 때 | 경로 최적화·장소검색 API 호출 시 예외 |
| `TourApi:ServiceKey` | `HotelInfo:Provider`를 `TourApi`로 바꿨을 때(기본은 `Mock`) | 숙소 검색이 Mock 데이터만 반환 |
| `Fcm:ServiceAccountJson` | `PushNotification:Provider`를 `Fcm`으로 바꿨을 때(기본은 `Log`, 실제 푸시 없이 로그만 남김) | 실제 FCM 푸시가 발송되지 않고 로그로 폴백 |

플레이스홀더가 남은 채로 기동되면 `SecretsConfigurationCheck`가 시작 시 경고 로그를 남겨 배포 실수를 방지합니다(단, `Anthropic:ApiKey`만은 경고가 아니라 즉시 종료).

---

# 공통

## JWT 인증

인증이 필요한 API는 다음 Header를 사용합니다.

```http
Authorization: Bearer {JWT_TOKEN}
```

JWT Claim:

| Claim | 설명 |
|---|---|
| NameIdentifier | 사용자 ID |
| Email | 사용자 이메일 |
| Name | 사용자 닉네임 |

토큰 유효기간: **1일(24시간)**

인증이 필요한 요청이 401을 받으면(로그인/회원가입 자체는 제외) 클라이언트가 로컬 토큰을 지우고 로그인 화면으로 돌아가도록 되어 있습니다. 서버 쪽에서 강제로 토큰을 무효화하는 별도 API는 없습니다(자연 만료만 있음).

---

# 1. User API

## Base URL

```text
/api/User
```

## 회원가입

```http
POST /api/User/register
```

Request:

```json
{
  "email": "test@example.com",
  "password": "123456",
  "nickname": "컴붕이1"
}
```

- `email`: 이메일 형식
- `password`: 최소 6자
- `nickname`: 2~15자

Success:

```http
200 OK
```

```json
{
  "email": "test@example.com",
  "nickname": "컴붕이1",
  "token": "JWT_TOKEN"
}
```

이메일 중복:

```http
400 Bad Request
```

```json
"이미 존재하는 계정입니다."
```

---

## 로그인

```http
POST /api/User/login
```

Request:

```json
{
  "email": "test@example.com",
  "password": "123456"
}
```

Success:

```http
200 OK
```

```json
{
  "email": "test@example.com",
  "nickname": "컴붕이1",
  "token": "JWT_TOKEN"
}
```

실패:

```http
401 Unauthorized
```

```json
"이메일 또는 비밀번호가 일치하지 않습니다."
```

---

## 내 정보 조회

```http
GET /api/User/me
```

인증 필요.

Success:

```json
{
  "message": "인증 성공",
  "email": "test@example.com",
  "nickname": "컴붕이1"
}
```

---

## FCM 디바이스 토큰 등록/갱신

```http
POST /api/User/device-token
```

인증 필요. 사용자당 최신 토큰 1개만 유지합니다(upsert). 같은 토큰이 **다른 계정**에 이미 등록되어 있으면(기기를 재설치하거나 계정을 바꿔 로그인한 경우) 그 행은 먼저 삭제한 뒤 현재 계정으로 등록합니다 — 그렇지 않으면 이전 계정도 이 기기로 가는 푸시를 계속 받게 됩니다.

Request:

```json
{
  "token": "fcm-device-token"
}
```

Success: `204 No Content`
토큰이 비어 있으면: `400 Bad Request`

---

## FCM 디바이스 토큰 삭제

```http
DELETE /api/User/device-token
```

인증 필요. 로그아웃 시 호출합니다. 현재 계정에 등록된 토큰을 삭제하며, 등록된 게 없어도 `204 No Content`를 반환합니다.

---

# 2. Trip API

## Base URL

```text
/api/Trip
```

모든 Trip API는 JWT 인증이 필요합니다.

## 여행 생성

```http
POST /api/Trip
```

여행 생성자는 JWT의 사용자 ID를 이용하며 자동으로 `Owner` 멤버가 됩니다.

Request:

```json
{
  "title": "오사카 여행",
  "startDate": "2026-09-10T00:00:00Z",
  "endDate": "2026-09-13T00:00:00Z"
}
```

Success:

```json
{
  "id": 1,
  "title": "오사카 여행",
  "startDate": "2026-09-10T00:00:00Z",
  "endDate": "2026-09-13T00:00:00Z",
  "createdAt": "2026-09-02T00:00:00Z"
}
```

종료일이 시작일보다 빠른 경우 `400 Bad Request`.

---

## 참여 중인 여행 목록 조회

```http
GET /api/Trip
```

현재 로그인한 사용자가 참여 중인 모든 여행을 조회합니다.

---

## 특정 여행 조회

```http
GET /api/Trip/{id}
```

해당 여행의 멤버만 조회할 수 있습니다.

---

## 여행 수정

```http
PUT /api/Trip/{id}
```

현재 구현 기준으로 **Owner만 수정 가능**합니다.

Request:

```json
{
  "title": "오사카 가족 여행",
  "startDate": "2026-09-11T00:00:00Z",
  "endDate": "2026-09-15T00:00:00Z"
}
```

---

## 여행 삭제

```http
DELETE /api/Trip/{id}
```

현재 구현 기준으로 **Owner만 삭제 가능**합니다.

Success:

```json
{
  "message": "여행이 삭제되었습니다."
}
```

---

## 예산 설정

```http
PUT /api/Trip/{tripId}/budget
```

여행 멤버라면 누구나 예산을 설정/변경할 수 있습니다. `budgetAmount`가 `null`이면 예산을 해제합니다.

Request:

```json
{
  "budgetAmount": 500000
}
```

Success: `204 No Content`
`budgetAmount`가 음수면 `400 Bad Request`.

---

## 예산 대비 잔액 조회

```http
GET /api/Trip/{tripId}/budget-summary
```

Success:

```json
{
  "budget": 500000,
  "totalSpent": 320000,
  "remaining": 180000,
  "isOverBudget": false
}
```

예산이 설정되어 있지 않으면 `budget`/`remaining`은 `null`이고 `isOverBudget`은 항상 `false`입니다.

---

## 카테고리별 지출 비율 조회

```http
GET /api/Trip/{tripId}/expense-breakdown
```

Success (지출 총액 기준 내림차순):

```json
[
  { "category": "FOOD", "total": 180000, "ratio": 0.5625 },
  { "category": "TRANSPORT", "total": 140000, "ratio": 0.4375 }
]
```

`category`는 `FOOD` / `TRANSPORT` / `LODGING` / `ETC` 중 하나입니다.

---

# 3. TripMember API

## 여행 멤버 추가

```http
POST /api/Trip/{id}/members
```

현재는 초대 수락 방식이 아니라 **Owner가 회원가입된 사용자를 이메일로 즉시 추가**하는 방식입니다.

Request:

```json
{
  "email": "friend@example.com"
}
```

Success:

```json
{
  "message": "여행 멤버가 추가되었습니다."
}
```

주요 오류:

- Owner가 아님 → `403 Forbidden`
- 사용자 없음 → `404 Not Found`
- 이미 멤버임 → `400 Bad Request`

---

## 여행 멤버 목록 조회

```http
GET /api/Trip/{id}/members
```

해당 여행의 멤버만 조회할 수 있습니다.

Success:

```json
[
  {
    "userId": 1,
    "email": "owner@example.com",
    "nickname": "방장",
    "role": "Owner"
  },
  {
    "userId": 2,
    "email": "friend@example.com",
    "nickname": "친구",
    "role": "Member"
  }
]
```

---

# 4. Schedule API

## Base URL

```text
/api/Trip/{tripId}/Schedule
```

모든 Schedule API는 JWT 인증이 필요하며 **해당 여행 멤버만 접근 가능**합니다.

## 일정 생성

```http
POST /api/Trip/{tripId}/Schedule
```

Request:

```json
{
  "title": "도톤보리 구경",
  "placeName": "도톤보리",
  "description": "저녁 먹고 주변 구경",
  "startTime": "2026-09-10T18:00:00Z",
  "endTime": "2026-09-10T20:00:00Z",
  "order": 1,
  "latitude": 34.6687,
  "longitude": 135.5013,
  "priority": 0,
  "isEssential": true,
  "isHotelCheckIn": false
}
```

- `latitude`/`longitude`: 경로 최적화 대상이 되려면 둘 다 있어야 함(선택)
- `priority`(정수) / `isEssential`(bool): 경로 최적화 Stage 2에서 시간이 부족할 때 어떤 일정을 뺄지 판단하는 데 쓰임. `isEssential=true`(기본값)면 항상 방문, `false`면 `priority`가 낮은 순으로 스킵 후보가 됨
- `isHotelCheckIn=true`면 경로 최적화에서 "이 시각 이전엔 방문 불가"한 앵커로 취급되고, 시간대 겹침 검사에서도 제외됨(체크인 기간과 다른 일정이 겹쳐도 허용)

조건:

- 종료 시간은 시작 시간보다 빠를 수 없음
- 일정은 여행 시작일~종료일 범위 안에 있어야 함
- `isHotelCheckIn=false`인 일정끼리는 같은 시간대에 겹칠 수 없음(겹치면 `400 Bad Request`, 겹치는 일정 제목/시간을 메시지로 안내)

Success:

```json
{
  "id": 1,
  "tripId": 1,
  "title": "도톤보리 구경",
  "placeName": "도톤보리",
  "description": "저녁 먹고 주변 구경",
  "startTime": "2026-09-10T18:00:00Z",
  "endTime": "2026-09-10T20:00:00Z",
  "order": 1,
  "latitude": 34.6687,
  "longitude": 135.5013,
  "priority": 0,
  "isEssential": true,
  "isHotelCheckIn": false,
  "createdAt": "2026-09-02T00:00:00Z"
}
```

---

## 여행 전체 일정 조회

```http
GET /api/Trip/{tripId}/Schedule
```

날짜(그룹) 오름차순, 같은 날짜 안에서는 `Order` 오름차순(경로 최적화 결과 반영), `Order`가 같으면 `StartTime` 오름차순으로 조회됩니다.

---

## 특정 일정 조회

```http
GET /api/Trip/{tripId}/Schedule/{scheduleId}
```

일정이 없으면:

```http
404 Not Found
```

```json
"일정을 찾을 수 없습니다."
```

---

## 일정 수정

```http
PUT /api/Trip/{tripId}/Schedule/{scheduleId}
```

Request 형식은 생성과 동일(`priority`/`isEssential`/`isHotelCheckIn` 포함). 수정된 일정 역시 여행 기간 내에 있어야 하고, 생성과 같은 시간대 겹침 검사를 거칩니다.

---

## 일정 삭제

```http
DELETE /api/Trip/{tripId}/Schedule/{scheduleId}
```

Success:

```json
{
  "message": "일정이 삭제되었습니다."
}
```

---

# 5. RouteOptimization API

## Base URL

```text
/api/Trip/{tripId}/RouteOptimization
```

인증 필요, 해당 여행 멤버만 접근 가능합니다.

## 특정 날짜의 최적 방문 순서 계산

```http
GET /api/Trip/{tripId}/RouteOptimization/{date}?apply=false&dayStartTime=09:00&dayEndTime=21:00
```

- `date`: `yyyy-MM-dd`
- `apply`(선택, 기본 `false`): `true`면 계산한 순서를 각 일정의 `Order`에 실제로 저장
- `dayStartTime`/`dayEndTime`(선택, `HH:mm`): 그날 활동에 배정 가능한 시간 범위. 안 주면 그날 등록된 일정들의 (가장 이른 시작)~(가장 늦은 종료) 범위를 기본 예산으로 씀

**1단계 — 최소 이동시간 순서**: 좌표가 있는 그날의 일정을 전부 방문하는 최소 이동시간 순서를 구합니다(N≤10이면 Held-Karp로 정확해, 그보다 많으면 OR-Tools → 유전 알고리즘 순으로 시도). 호텔 체크인(`isHotelCheckIn=true`) 일정은 "그 시각 이전엔 방문 불가"한 앵커로 취급되어 순서가 후처리로 조정됩니다.

**2단계 — 우선순위 기반 시간예산 스킵**: 위 결과(이동시간 + 각 일정의 활동시간 합)가 시간 예산을 넘을 것으로 예상되면, `isEssential=false`인 일정 중 `priority`가 낮은 것부터 하나씩 제외하며 다시 계산합니다. 앵커(호텔 체크인)는 우선순위와 무관하게 항상 포함됩니다. 스킵 가능한 일정을 전부 빼도 예산을 넘으면(남은 게 전부 필수/앵커), 더 빼지 않고 전부 포함한 채 `exceedsTimeBudget=true`로만 알립니다.

Success:

```json
{
  "date": "2026-09-10T00:00:00",
  "stops": [
    {
      "scheduleId": 3,
      "title": "도톤보리 구경",
      "placeName": "도톤보리",
      "latitude": 34.6687,
      "longitude": 135.5013,
      "visitOrder": 0,
      "travelTimeFromPreviousSeconds": 0,
      "isHotelCheckIn": false
    }
  ],
  "totalTravelTimeSeconds": 1830,
  "solverUsed": "HeldKarp",
  "applied": false,
  "skippedSchedulesWithoutCoordinates": ["좌표 없는 일정 제목"],
  "anchorAdjusted": false,
  "skippedSchedulesByPriority": [
    { "scheduleId": 7, "title": "쇼핑몰 구경", "priority": 0, "reason": "예상 소요 시간이 하루 활동 시간 예산을 초과해 우선순위가 낮은 일정부터 제외됨" }
  ],
  "exceedsTimeBudget": false
}
```

해당 날짜에 일정이 없으면 `404 Not Found`.

---

# 6. AiChat API

## Base URL

```text
/api/Trip/{tripId}/AiChat
```

인증 필요, 해당 여행 멤버만 접근 가능합니다. LLM은 Anthropic Messages API를 실제로 호출합니다(`AnthropicLlmClient`, 모델은 `appsettings.json`의 `Anthropic:Model`로 설정, 기본 `claude-haiku-4-5-20251001`).

## 대화 세션 생성

```http
POST /api/Trip/{tripId}/AiChat/sessions
```

Success:

```json
{
  "sessionId": 1,
  "tripId": 1,
  "createdAt": "2026-09-10T09:00:00Z"
}
```

## 세션에 메시지 보내기 (장소 추천)

```http
POST /api/Trip/{tripId}/AiChat/sessions/{sessionId}/messages
```

세션 소유자 본인만 호출 가능합니다.

Request:

```json
{
  "message": "맛집 근처로 반나절 코스 추천해줘",
  "currentLatitude": 37.5665,
  "currentLongitude": 126.9780
}
```

- `currentLatitude`/`currentLongitude`(선택): "근처/주변" 같은 질의에 쓰이는 현재 GPS 좌표. 위치 권한이 없으면 `null`로 보냄

동작 순서: ① 질문 분석(의도/위치/카테고리 판단) → ② 후보 검색(Tmap 기반, 분석 결과로 검색 기준점을 정하고 주변 후보를 모음 — "강남 맛집"이면 GPS가 와도 강남 기준) → ③ LLM은 검색된 후보 목록 중에서만 골라 설명 → ④ 검증(그라운딩): 후보 목록에 없는 장소는 걸러냄. 기준점을 못 정했거나 후보가 없거나 검색 자체가 실패하면 LLM을 아예 부르지 않고 이유를 바로 답합니다(환각 방지).

Success:

```json
{
  "replyText": "광장시장을 추천드려요!",
  "searchStatus": "Success",
  "searchLocation": "강남역",
  "recommendations": [
    {
      "placeName": "광장시장",
      "description": "다양한 길거리 음식을 맛볼 수 있는 전통 시장이에요.",
      "suggestedStartTime": "2026-09-10T10:00:00",
      "suggestedEndTime": "2026-09-10T12:00:00",
      "latitude": 37.5701,
      "longitude": 126.9986,
      "imageUrl": null
    }
  ]
}
```

- `searchStatus`: `Success` / `NotRecommendation`(추천 요청이 아님) / `LocationUnavailable` / `NoCandidates` / `SearchUnavailable` / `GroundingFailed` 중 하나. `Success`/`NotRecommendation`이 아니면 `recommendations`는 항상 빈 배열
- `imageUrl`은 LLM이 아니라 서버의 `IPlaceImageProvider`(설정 `PlaceImage:Provider`, 기본 `Mock` = 항상 null)가 검증된 추천에만 채움. null이면 프론트가 `placeName`으로 Google Places API를 별도 조회하거나 "사진 없음"을 보여줌
- 대화 이력은 세션마다 최근 16개 메시지로 슬라이딩 윈도우 관리

LLM 호출 실패 시 `502 Bad Gateway`.

---

# 7. HotelInfo API

## Base URL

```text
/api/HotelInfo
```

인증 필요. 여행(Trip)과 무관한 공용 정보라 Trip 하위 라우트가 아닙니다. 한국관광공사 TourAPI로 조회하며, 가격·잔여객실·예약은 다루지 않습니다(예약은 앱이 아고다/트립닷컴 등 외부 사이트로 넘김).

## 숙박시설 검색

```http
GET /api/HotelInfo/search?keyword=...
GET /api/HotelInfo/search?areaCode=1&sigunguCode=23
GET /api/HotelInfo/search?lat=37.5&lng=127.0&radius=2000
```

`keyword` / `areaCode` / `lat+lng` 중 **정확히 하나**의 검색 방식만 지정해야 합니다(`sigunguCode`는 `areaCode` 검색 시 선택, `radius`는 위치 검색 시 선택·기본 2000m·최대값은 provider 설정에 따름). `page`(기본 1)로 페이지네이션.

Success: `HotelListItemDto` 배열. 외부 API 실패 시 `502 Bad Gateway`.

## 숙박시설 상세 조회

```http
GET /api/HotelInfo/{contentId}
```

없으면 `404 Not Found`, 외부 API 실패 시 `502 Bad Gateway`.

---

# 8. Expense API (가계부)

## Base URL

```text
/api/Trip/{tripId}/expenses, /api/Expense/{id}
```

인증 필요, 해당 여행 멤버만 접근 가능합니다.

## 지출 등록

```http
POST /api/Trip/{tripId}/expenses
```

Request(균등분할):

```json
{
  "paidByUserId": 2,
  "amount": 30000,
  "category": "FOOD",
  "date": "2026-09-10T12:00:00Z",
  "merchantName": "스시로",
  "memo": "점심",
  "scheduleId": null,
  "splitMemberIds": [1, 2]
}
```

Request(커스텀 분담 — `splits`가 있으면 `splitMemberIds`는 무시):

```json
{
  "paidByUserId": 1,
  "amount": 10000,
  "category": "TRANSPORT",
  "date": "2026-09-10T09:00:00Z",
  "splits": [
    { "userId": 1, "shareAmount": 4000 },
    { "userId": 2, "shareAmount": 6000 }
  ]
}
```

- 균등분할은 소수 둘째 자리까지 내림 처리하고, 나머지는 결제자(분담자에 포함된 경우) 또는 첫 번째 멤버가 부담
- 커스텀 분담은 `shareAmount` 합계가 `amount`와 정확히 같아야 함(다르면 `400 Bad Request`)
- `paidByUserId`와 분담 대상은 모두 여행 멤버여야 함
- `scheduleId`(선택)를 주면 해당 여행의 일정과 연결(다른 여행 일정이면 `400 Bad Request`)
- 이 지출로 예산이 "이내 → 초과"로 바뀌는 순간에만 여행 멤버 전원에게 "예산 초과" 알림 발송(이미 초과 상태였으면 추가로 알리지 않음)

Success: `ExpenseResponseDto`(등록된 지출 + `splits` 포함)

## 지출 목록 조회

```http
GET /api/Trip/{tripId}/expenses
```

날짜 내림차순, 같으면 `Id` 내림차순. 각 지출에 `splits`가 함께 포함됩니다.

## 지출 단건 조회

```http
GET /api/Expense/{id}
```

## 지출 수정

```http
PUT /api/Expense/{id}
```

Request는 등록과 동일한 형식. **분담 내역은 통째로 교체**됩니다(기존 분담을 지우고 요청받은 분담으로 다시 씀). 예산 초과 알림도 등록과 같은 규칙(전환 순간에만)을 따르며, 수정 전 상태를 먼저 계산해 비교합니다.

## 지출 삭제

```http
DELETE /api/Expense/{id}
```

Success: `204 No Content`

## 더치페이 정산 결과 조회(실시간)

```http
GET /api/Trip/{tripId}/settlement
```

현재 등록된 지출을 기준으로 최소 송금 횟수가 되도록 계산합니다(지출이 바뀌면 결과도 바로 바뀜 — 확정된 결과를 보려면 아래 확정 API 참고).

Success:

```json
[
  { "fromUserId": 2, "toUserId": 1, "amount": 4000 }
]
```

## 정산 확정

```http
POST /api/Trip/{tripId}/Expense/settlements/finalize
```

현재 정산 결과를 스냅샷으로 저장하고, 확정한 본인을 제외한 같은 여행 멤버 전원에게 "정산 결과 도착" 알림을 보냅니다. **멱등**: 방금 계산한 결과가 이 여행의 가장 최근 확정본과 완전히 같으면 새 스냅샷을 또 만들거나 알림을 다시 보내지 않고 기존 확정본을 그대로 돌려줍니다. 확정 이후 지출이 추가/수정돼 결과가 달라지면 그때만 새로 확정됩니다.

Success: `SettlementDto`(아래 조회 API와 동일한 형식)

## 확정된 정산 결과 조회

```http
GET /api/Trip/{tripId}/Expense/settlements/{settlementId}
```

확정 시점의 스냅샷을 그대로 반환합니다(이후 지출이 바뀌어도 변하지 않음). 본인이 속한 여행이 아니면 `403 Forbidden`, 없으면 `404 Not Found`.

Success:

```json
{
  "id": 5,
  "tripId": 1,
  "finalizedByUserId": 1,
  "finalizedAt": "2026-09-13T10:00:00Z",
  "transfers": [
    { "fromUserId": 2, "toUserId": 1, "amount": 4000 }
  ]
}
```

---

# 9. 데이터 관계

```text
User ──< TripMember >── Trip ──< Schedule
                          │
                          ├──< Expense >──< ExpenseSplit
                          ├──< Settlement
                          └──< ChatSession
User ──< DeviceToken (1:1, 계정당 최신 토큰 1개)
```

- 한 사용자는 여러 여행에 참여 가능, 한 여행에는 여러 사용자가 참여 가능(`TripMember`가 다대다 연결, `Role`은 `Owner`/`Member`, 동일 `(TripId, UserId)` 중복 불가)
- 하나의 Trip에는 여러 Schedule, Expense, ChatSession, Settlement가 존재 가능
- Expense는 결제자(`PaidByUserId`)와 분담 내역(`ExpenseSplit` 목록)을 가지며, 선택적으로 Schedule과 연결 가능
- Settlement는 확정 시점의 정산 결과를 JSON 스냅샷(`TransfersJson`)으로 저장
- DeviceToken은 사용자당 최신 FCM 토큰 1개만 유지

---

# 10. 현재 구현 Endpoint 요약

| Method | Endpoint | 인증 | 권한 | 설명 |
|---|---|---|---|---|
| POST | `/api/User/register` | X | - | 회원가입 |
| POST | `/api/User/login` | X | - | 로그인 |
| GET | `/api/User/me` | O | 로그인 사용자 | 내 정보 조회 |
| POST | `/api/User/device-token` | O | 로그인 사용자 | FCM 토큰 등록/갱신 |
| DELETE | `/api/User/device-token` | O | 로그인 사용자 | FCM 토큰 삭제(로그아웃) |
| POST | `/api/Trip` | O | 로그인 사용자 | 여행 생성 |
| GET | `/api/Trip` | O | 로그인 사용자 | 참여 여행 목록 |
| GET | `/api/Trip/{id}` | O | 여행 멤버 | 특정 여행 조회 |
| PUT | `/api/Trip/{id}` | O | Owner | 여행 수정 |
| DELETE | `/api/Trip/{id}` | O | Owner | 여행 삭제 |
| PUT | `/api/Trip/{tripId}/budget` | O | 여행 멤버 | 예산 설정 |
| GET | `/api/Trip/{tripId}/budget-summary` | O | 여행 멤버 | 예산 대비 잔액 |
| GET | `/api/Trip/{tripId}/expense-breakdown` | O | 여행 멤버 | 카테고리별 지출 비율 |
| POST | `/api/Trip/{id}/members` | O | Owner | 여행 멤버 추가 |
| GET | `/api/Trip/{id}/members` | O | 여행 멤버 | 멤버 목록 조회 |
| POST | `/api/Trip/{tripId}/Schedule` | O | 여행 멤버 | 일정 생성 |
| GET | `/api/Trip/{tripId}/Schedule` | O | 여행 멤버 | 전체 일정 조회 |
| GET | `/api/Trip/{tripId}/Schedule/{scheduleId}` | O | 여행 멤버 | 특정 일정 조회 |
| PUT | `/api/Trip/{tripId}/Schedule/{scheduleId}` | O | 여행 멤버 | 일정 수정 |
| DELETE | `/api/Trip/{tripId}/Schedule/{scheduleId}` | O | 여행 멤버 | 일정 삭제 |
| GET | `/api/Trip/{tripId}/RouteOptimization/{date}` | O | 여행 멤버 | 최적 방문 순서 계산(+우선순위 스킵) |
| POST | `/api/Trip/{tripId}/AiChat/sessions` | O | 여행 멤버 | AI 챗 세션 생성 |
| POST | `/api/Trip/{tripId}/AiChat/sessions/{sessionId}/messages` | O | 세션 소유자 | 장소 추천 챗봇에 메시지 전송 |
| GET | `/api/HotelInfo/search` | O | 로그인 사용자 | 숙박시설 검색 |
| GET | `/api/HotelInfo/{contentId}` | O | 로그인 사용자 | 숙박시설 상세 조회 |
| POST | `/api/Trip/{tripId}/expenses` | O | 여행 멤버 | 지출 등록 |
| GET | `/api/Trip/{tripId}/expenses` | O | 여행 멤버 | 지출 목록 조회 |
| GET | `/api/Expense/{id}` | O | 여행 멤버 | 지출 단건 조회 |
| PUT | `/api/Expense/{id}` | O | 여행 멤버 | 지출 수정 |
| DELETE | `/api/Expense/{id}` | O | 여행 멤버 | 지출 삭제 |
| GET | `/api/Trip/{tripId}/settlement` | O | 여행 멤버 | 정산 결과(실시간) |
| POST | `/api/Trip/{tripId}/Expense/settlements/finalize` | O | 여행 멤버 | 정산 확정(멱등) |
| GET | `/api/Trip/{tripId}/Expense/settlements/{settlementId}` | O | 여행 멤버 | 확정된 정산 조회 |

---

# 11. 기본 사용 흐름

```text
회원가입 / 로그인
        ↓
JWT 발급 + FCM 디바이스 토큰 등록
        ↓
여행 생성 (생성자 Owner 자동 등록) → 다른 사용자 멤버 추가
        ↓
일정 생성 / 조회 / 수정 / 삭제, AI 챗봇으로 장소추천 받아 일정에 반영
        ↓
날짜별 경로 최적화(이동시간 최소화 + 시간예산 초과 시 우선순위 낮은 일정 자동 제외)
        ↓
지출 등록(더치페이 분담) → 예산 대비 확인 → 정산 계산 → 정산 확정(멤버에게 알림)
        ↓
로그아웃(FCM 토큰 정리 후 로컬 토큰 삭제)
```

---

# 12. 다음 구현 후보

- 여행 멤버 삭제 / 일반 멤버 여행 나가기 / Owner 권한 이전
- 초대 수락 · 거절 방식의 Invitation 기능
- 날짜별 일정 조회 전용 API(현재는 전체 조회 후 클라이언트에서 그룹핑)
- Tmap route-matrix 배치 API로 경로 최적화 이동시간 조회 전환(실시간 정체 반영 여부 확인 필요)
- 야놀자/여기어때 예약 딥링크(정확한 검색 URL 패턴 확인 필요)
- 지출/정산 예산 설정 권한을 Owner 전용으로 제한할지 여부 결정(현재는 여행 멤버 누구나 가능)
