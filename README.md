# AI 장소추천 챗봇 (`feature/ai-chatbot`)

여행 지도 화면의 **"장소추천"** 버튼으로 진입하는 AI 챗봇 기능입니다. 이미 등록된 여행 일정을 참고해, 대화로 받은 사용자 취향에 맞는 장소를 추천하고 일정에 바로 추가할 수 있습니다.

## 동작 흐름

1. 지도 화면에서 **장소추천** 버튼 탭 → 챗봇 세션 생성
2. 사용자가 대화로 취향을 말함 (예: "조용한 카페 추천해줘")
3. AI가 해당 여행의 **기존 일정과 겹치지 않는 시간대**로 장소를 1~3개 추천
4. 추천 카드에 **장소명 + 대표 사진 + "일정에 추가" 버튼**이 함께 표시됨
5. "일정에 추가" 클릭 → **방문 시간 입력 다이얼로그**(AI 제안 시간이 기본값으로 채워짐) → 확인 시 기존 일정 생성 API로 저장

## 백엔드 (`backend/`)

| 구성 요소 | 위치 | 설명 |
|---|---|---|
| `ChatSession` 엔티티 | `SharedData/Models/ChatSession.cs` | 대화 세션 저장 (TripId, UserId, 대화 이력 JSON) |
| `AiChatController` | `TravelApp.WebAPI/Controllers/AiChatController.cs` | 세션 생성, 메시지 전송 API |
| `AnthropicLlmClient` | `TravelApp.WebAPI/Services/Llm/AnthropicLlmClient.cs` | Anthropic Messages API 직접 호출 (HttpClient) |
| `AiChatPromptBuilder` | `TravelApp.WebAPI/Services/AiChatPromptBuilder.cs` | 여행 정보 + 기존 일정을 시스템 프롬프트로 구성 |
| `AnthropicStartupValidator` | `TravelApp.WebAPI/Services/Llm/AnthropicStartupValidator.cs` | API 키 미설정 시 앱 시작 시점에 즉시 실패 처리 |

### API

```
POST /api/Trip/{tripId}/AiChat/sessions
POST /api/Trip/{tripId}/AiChat/sessions/{sessionId}/messages
```

둘 다 `[Authorize]` + 여행 멤버 검증(`IsTripMember`)을 거칩니다. 두 번째 엔드포인트는 세션 소유자 본인만 호출 가능합니다.

### 추천 응답 스키마

```json
{
  "replyText": "string",
  "recommendations": [
    {
      "placeName": "string",
      "description": "string",
      "suggestedStartTime": "yyyy-MM-ddTHH:mm:ssZ",
      "suggestedEndTime": "yyyy-MM-ddTHH:mm:ssZ"
    }
  ]
}
```

일정 저장은 이 응답을 그대로 쓰지 않고, 사용자가 시간을 확인/수정한 뒤 **기존 `POST /api/Trip/{tripId}/Schedule` API**를 호출하는 방식입니다. 새 저장 로직을 추가하지 않고 기존 검증(여행 기간 밖 등록 방지 등)을 그대로 재사용합니다.

### 사용 모델

- 기본값: `claude-haiku-4-5-20251001` (비용 효율 우선)
- `appsettings.json`의 `Anthropic:Model` 값으로 다른 모델(예: `claude-sonnet-5`)로 교체 가능. 별도 코드 수정 불필요.

### 보안

- 시스템 프롬프트에 프롬프트 인젝션 방어 문구 포함 (사용자 메시지를 항상 데이터로만 취급, 역할/스키마 이탈 지시 무시)
- 대화 이력은 최근 16개 메시지로 슬라이딩 윈도우 관리
- `Anthropic:ApiKey`는 코드/설정 파일에 하드코딩하지 않고 `dotnet user-secrets`로만 관리

## 프론트엔드 (`frontend/testBuild01/`)

| 구성 요소 | 위치 | 설명 |
|---|---|---|
| `AiChatScreen` | `ui/aichat/AiChatScreen.kt` | 대화창 + 추천 카드 UI |
| `AiChatViewModel` | `ui/aichat/AiChatViewModel.kt` | 세션/대화 상태 관리, API 연동 |
| `PlacesRepository` | `data/repository/PlacesRepository.kt` | Google Places API로 추천 장소의 대표 사진 조회 |
| `PlacesApiService` | `data/network/PlacesApiService.kt` | Places "Find Place from Text" Retrofit 인터페이스 |

추천 카드의 사진은 AI 응답에 포함하지 않고, `placeName`으로 Google Places API를 별도 호출해 가져옵니다 (LLM이 존재하지 않는 이미지 URL을 생성하는 것을 방지).

## 실행 전 준비

**백엔드**
```
cd backend/TravelApp.WebAPI
dotnet user-secrets set "Anthropic:ApiKey" "발급받은_키"
dotnet ef database update
```

**프론트엔드**

`local.properties`에 `google_maps_api_key`가 등록되어 있어야 하며, 해당 키에 **Maps SDK for Android**와 **Places API**가 모두 활성화되어 있어야 합니다.

## 알려진 제한사항

- 서버는 일정 **기간**(날짜) 밖 등록만 막고, 같은 날 **시간대 겹침**은 검증하지 않습니다. 현재는 시스템 프롬프트로만 겹치지 않게 유도하고 있습니다.
- `TripController.UpdateTrip`의 라우트가 `[HttpPut("id")]`로 되어있어(`{id}`가 아님) 여행 정보 수정 API가 정상 동작하지 않습니다. 이 브랜치 범위 밖의 기존 이슈입니다.
- Tmap POI 응답에는 휴관/임시휴업 여부 필드가 없어 `PlaceRecommendationGrounder.IsOperatingAsOf`가 이를 감지하지 못합니다(예: 국립한글박물관 휴관 사례). 별도 데이터 소스(네이버 플레이스, 공공 API 등) 연동이 필요한 별개 작업으로 분리했습니다.

## 테스트

- Mock 응답으로 UI/UX 흐름 검증 후 실제 Anthropic API로 교체
- 프롬프트 인젝션(시스템 프롬프트 노출 시도, 역할 이탈 유도, JSON 스키마 이탈 유도 등) 수동 테스트 완료
