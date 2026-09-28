# TravelApp API 명세서

현재 구현된 **User / Trip / TripMember / Schedule API** 기준 명세서입니다.

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
  "order": 1
}
```

조건:

- 종료 시간은 시작 시간보다 빠를 수 없음
- 일정은 여행 시작일~종료일 범위 안에 있어야 함

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
  "createdAt": "2026-09-02T00:00:00Z"
}
```

---

## 여행 전체 일정 조회

```http
GET /api/Trip/{tripId}/Schedule
```

일정은 `StartTime` 오름차순, 시작 시간이 같으면 `Order` 순으로 조회됩니다.

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

Request:

```json
{
  "title": "도톤보리 야경 구경",
  "placeName": "도톤보리",
  "description": "저녁 식사 후 야경 보기",
  "startTime": "2026-09-10T19:00:00Z",
  "endTime": "2026-09-10T21:00:00Z",
  "order": 2
}
```

수정된 일정 역시 여행 기간 내에 있어야 합니다.

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

# 5. AiChat API

## Base URL

```text
/api/Trip/{tripId}/AiChat
```

인증 필요, 해당 여행 멤버만 접근 가능합니다.

## 장소 추천 챗봇에게 메시지 보내기

```http
POST /api/Trip/{tripId}/AiChat
```

Request:

```json
{
  "message": "맛집 근처로 반나절 코스 추천해줘"
}
```

Success:

```json
{
  "replyText": "'맛집 근처로 반나절 코스 추천해줘'와 어울리는 장소를 추천해드릴게요!",
  "recommendations": [
    {
      "placeName": "광장시장",
      "description": "다양한 길거리 음식을 맛볼 수 있는 전통 시장이에요.",
      "suggestedStartTime": "2026-09-10T10:00:00",
      "suggestedEndTime": "2026-09-10T12:00:00",
      "imageUrl": null
    }
  ]
}
```

- `imageUrl`은 LLM이 아니라 서버의 `IPlaceImageProvider`(설정 `PlaceImage:Provider`, 기본 `Mock` = 항상 null)가 검증된 추천에만 채웁니다. null이면 프론트가 `placeName`으로 Google Places API를 별도 조회하거나 "사진 없음"을 보여줍니다.
- 현재는 고정 후보군에서 골라 응답하는 Mock 구현(`MockAiRecommendationService`)이며, `IAiRecommendationService` 인터페이스만 유지하면 실제 LLM 연동 구현체로 교체할 수 있습니다.

---

# 6. 데이터 관계

```text
User ──< TripMember >── Trip ──< Schedule
```

- 한 사용자는 여러 여행에 참여 가능
- 한 여행에는 여러 사용자가 참여 가능
- `TripMember`가 User와 Trip 사이의 다대다 관계를 연결
- `TripMember.Role`: `Owner`, `Member`
- 동일한 `(TripId, UserId)` 조합은 중복 등록 불가
- 하나의 Trip에는 여러 Schedule이 존재 가능

---

# 7. 현재 구현 Endpoint 요약

| Method | Endpoint | 인증 | 권한 | 설명 |
|---|---|---|---|---|
| POST | `/api/User/register` | X | - | 회원가입 |
| POST | `/api/User/login` | X | - | 로그인 |
| GET | `/api/User/me` | O | 로그인 사용자 | 내 정보 조회 |
| POST | `/api/Trip` | O | 로그인 사용자 | 여행 생성 |
| GET | `/api/Trip` | O | 로그인 사용자 | 참여 여행 목록 |
| GET | `/api/Trip/{id}` | O | 여행 멤버 | 특정 여행 조회 |
| PUT | `/api/Trip/{id}` | O | Owner | 여행 수정 |
| DELETE | `/api/Trip/{id}` | O | Owner | 여행 삭제 |
| POST | `/api/Trip/{id}/members` | O | Owner | 여행 멤버 추가 |
| GET | `/api/Trip/{id}/members` | O | 여행 멤버 | 멤버 목록 조회 |
| POST | `/api/Trip/{tripId}/Schedule` | O | 여행 멤버 | 일정 생성 |
| GET | `/api/Trip/{tripId}/Schedule` | O | 여행 멤버 | 전체 일정 조회 |
| GET | `/api/Trip/{tripId}/Schedule/{scheduleId}` | O | 여행 멤버 | 특정 일정 조회 |
| PUT | `/api/Trip/{tripId}/Schedule/{scheduleId}` | O | 여행 멤버 | 일정 수정 |
| DELETE | `/api/Trip/{tripId}/Schedule/{scheduleId}` | O | 여행 멤버 | 일정 삭제 |
| POST | `/api/Trip/{tripId}/AiChat` | O | 여행 멤버 | 장소 추천 챗봇에 메시지 전송 |

---

# 8. 기본 사용 흐름

```text
회원가입 / 로그인
        ↓
JWT 발급
        ↓
여행 생성
        ↓
생성자 Owner 자동 등록
        ↓
다른 사용자 멤버 추가
        ↓
참여 여행 및 멤버 조회
        ↓
여행 일정 생성 / 조회 / 수정 / 삭제
```

---

# 9. 다음 구현 후보

- 여행 멤버 삭제
- 일반 멤버 여행 나가기
- Owner 권한 이전
- 초대 수락 / 거절 방식의 Invitation 기능
- 날짜별 일정 조회
- 일정 순서 변경 전용 API
- AiChat 실제 LLM 연동 (`IAiRecommendationService` 구현체 교체)
