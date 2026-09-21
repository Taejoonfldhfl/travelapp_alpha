using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedData.DTOs;
using SharedData.Models;
using System.Security.Claims;
using System.Text.Json;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services;
using TravelApp.WebAPI.Services.Llm;
using TravelApp.WebAPI.Services.PlaceSearch;

namespace TravelApp.WebAPI.Controllers
{
    [Route("api/Trip/{tripId}/[controller]")]
    [ApiController]
    [Authorize]
    public class AiChatController : ControllerBase
    {
        private const int MaxHistoryMessages = 16;

        // "근처/주변" 등 사용자의 현재 위치를 기준으로 한 검색의 반경.
        private const int NearbySearchRadiusMeters = 2000;

        // 위치 문구가 없을 때, 이미 좌표가 있는 일정들의 무게중심을 기준으로 쓰는 반경.
        // (여행지 전역을 아우를 수 있도록 근처 검색보다 넓게 잡는다.)
        private const int TripAreaSearchRadiusMeters = 15000;

        private static readonly JsonSerializerOptions ResponseJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ApplicationDbContext _context;
        private readonly AnthropicLlmClient _llmClient;
        private readonly INearbyPlaceSearchProvider _placeSearchProvider;

        public AiChatController(
            ApplicationDbContext context,
            AnthropicLlmClient llmClient,
            INearbyPlaceSearchProvider placeSearchProvider)
        {
            _context = context;
            _llmClient = llmClient;
            _placeSearchProvider = placeSearchProvider;
        }

        private async Task<bool> IsTripMember(int tripId, int userId)
        {
            return await _context.TripMembers
                .AnyAsync(tm =>
                    tm.TripId == tripId &&
                    tm.UserId == userId
                );
        }

        // 1. AI 장소추천 대화 세션 생성
        [HttpPost("sessions")]
        public async Task<ActionResult<ChatSessionCreateResponseDto>> CreateSession(int tripId)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            if (!await IsTripMember(tripId, userId))
            {
                return Forbid();
            }

            var trip = await _context.Trips.FindAsync(tripId);

            if (trip == null)
            {
                return NotFound("여행을 찾을 수 없습니다.");
            }

            // 세션 생성 시점에 여행/일정 컨텍스트가 조회 가능한 상태인지 확인한다.
            // (실제 시스템 프롬프트는 매 메시지 전송 시 최신 일정 기준으로 다시 구성한다.)
            var session = new ChatSession
            {
                TripId = tripId,
                UserId = userId,
                SlotsJson = "{}",
                MessagesJson = JsonSerializer.Serialize(new List<ChatHistoryMessage>())
            };

            _context.ChatSessions.Add(session);
            await _context.SaveChangesAsync();

            return Ok(new ChatSessionCreateResponseDto
            {
                SessionId = session.Id,
                TripId = session.TripId,
                CreatedAt = session.CreatedAt
            });
        }

        // 2. 세션에 메시지를 보내고 장소 추천을 받는다.
        [HttpPost("sessions/{sessionId}/messages")]
        public async Task<ActionResult<AiChatResponseDto>> SendMessage(
            int tripId,
            int sessionId,
            AiChatRequestDto request)
        {
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            if (!await IsTripMember(tripId, userId))
            {
                return Forbid();
            }

            var trip = await _context.Trips.FindAsync(tripId);

            if (trip == null)
            {
                return NotFound("여행을 찾을 수 없습니다.");
            }

            var session = await _context.ChatSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.TripId == tripId);

            if (session == null)
            {
                return NotFound("채팅 세션을 찾을 수 없습니다.");
            }

            if (session.UserId != userId)
            {
                return Forbid();
            }

            var schedules = await _context.Schedules
                .Where(s => s.TripId == tripId)
                .OrderBy(s => s.StartTime)
                .ThenBy(s => s.Order)
                .ToListAsync();

            // 검색 중심점(GPS 또는 좌표가 있는 일정들의 무게중심)이 있으면 "현재 위치 근처 후보"를
            // 미리 찾아 참고 목록으로 준다. 중심점이 없어도(둘 다 없어도) 더 이상 여기서 바로 막지
            // 않는다 — 사용자가 메시지에서 특정 장소를 언급했다면 그 장소를 기준으로 한 추천은
            // 현재 위치와 무관하게 가능해야 하고(아래에서 이름 검색으로 검증), 그런 언급도 없이
            // '근처'만 물었는데 위치 정보가 전혀 없는 경우는 프롬프트가 빈 후보 목록을 보고
            // LLM이 스스로 위치 정보가 필요하다고 안내하도록 한다.
            var searchAnchor = ResolveSearchAnchor(request, schedules, out int searchRadiusMeters, out var anchorSource);

            var candidatePlaces = searchAnchor != null
                ? await _placeSearchProvider.SearchNearbyAsync(
                    new NearbyPlaceSearchRequest(searchAnchor.Value.Latitude, searchAnchor.Value.Longitude, searchRadiusMeters, Keyword: null),
                    HttpContext.RequestAborted)
                : new List<PlaceSearchResultDto>();

            var history = DeserializeHistory(session.MessagesJson);
            history.Add(new ChatHistoryMessage { Role = "user", Content = request.Message });
            history = TrimToSlidingWindow(history);

            var systemPrompt = AiChatPromptBuilder.BuildSystemPrompt(trip, schedules, candidatePlaces, anchorSource);

            string rawReply;
            try
            {
                rawReply = await _llmClient.SendAsync(systemPrompt, history, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(502, $"AI 응답을 가져오지 못했습니다: {ex.Message}");
            }

            var parsedResponse = TryParseRecommendation(rawReply);

            if (parsedResponse == null)
            {
                return StatusCode(502, "AI 응답을 해석하지 못했습니다.");
            }

            // 규칙 2 집행: 추천 하나하나를 실제 좌표로 검증하고, 검증되지 않으면(환각이거나
            // 좌표를 확정할 수 없으면) 그 추천만 버린다. 또한 폐업이 확인됐거나 영업 확인 정보가
            // 3개월보다 오래된 장소도(질문한 시점 기준, IsOperatingAsOf) 신뢰할 수 없으므로 버린다.
            // 1) 먼저 "현재 위치 근처 후보 목록"과 이름이 일치하는지 본다(빠름, 이미 조회해둔 데이터).
            // 2) 일치하지 않으면(=사용자가 특정 장소를 언급해 후보 목록 밖의 이름일 가능성) 장소
            //    검색 API의 이름 검색으로 실제 존재 여부/좌표/영업 여부를 다시 확인한다 — 이 경로는
            //    사용자의 현재 위치와 무관하게 동작하므로, GPS가 없어도 특정 장소 기준 추천이 가능하다.
            int rawCount = parsedResponse.Recommendations.Count;
            var groundedRecommendations = new List<AiPlaceRecommendationDto>();
            var askedAt = DateTime.UtcNow;

            foreach (var recommendation in parsedResponse.Recommendations)
            {
                if (PlaceRecommendationGrounder.TryMatch(recommendation.PlaceName, candidatePlaces, out var candidateMatch) &&
                    PlaceRecommendationGrounder.IsOperatingAsOf(candidateMatch!, askedAt))
                {
                    recommendation.PlaceId = candidateMatch!.PlaceId;
                    recommendation.Latitude = candidateMatch.Latitude;
                    recommendation.Longitude = candidateMatch.Longitude;
                    groundedRecommendations.Add(recommendation);
                    continue;
                }

                var nameMatch = await _placeSearchProvider.SearchByNameAsync(recommendation.PlaceName, HttpContext.RequestAborted);

                if (nameMatch != null && PlaceRecommendationGrounder.IsOperatingAsOf(nameMatch, askedAt))
                {
                    recommendation.PlaceId = nameMatch.PlaceId;
                    recommendation.Latitude = nameMatch.Latitude;
                    recommendation.Longitude = nameMatch.Longitude;
                    groundedRecommendations.Add(recommendation);
                }
                // 못 찾았거나(환각), 찾았지만 폐업/오래된 정보라면 버리고 다른 추천만 보여준다.
            }

            parsedResponse.Recommendations = groundedRecommendations;

            if (rawCount > 0 && parsedResponse.Recommendations.Count == 0)
            {
                parsedResponse.ReplyText +=
                    " (죄송해요, 좌표가 확실한 장소를 찾지 못해 추천을 보여드리지 못했어요. 더 구체적으로 말씀해주시겠어요?)";
            }

            history.Add(new ChatHistoryMessage { Role = "assistant", Content = rawReply });
            history = TrimToSlidingWindow(history);

            session.MessagesJson = JsonSerializer.Serialize(history);
            await _context.SaveChangesAsync();

            return Ok(parsedResponse);
        }

        // GPS 좌표가 있으면 그 좌표를(좁은 반경), 없으면 좌표가 있는 기존 일정들의 무게중심을
        // (넓은 반경) 검색 중심점으로 쓴다. 둘 다 없으면 null.
        // GPS는 메시지에 "근처" 같은 표현이 없어도(=사용자가 특정 장소를 언급하지 않아도)
        // 항상 우선한다 — 이렇게 해야 "근처"라고 말하지 않아도 현재 위치 기준 추천이 된다.
        private static (double Latitude, double Longitude)? ResolveSearchAnchor(
            AiChatRequestDto request, List<Schedule> schedules, out int searchRadiusMeters, out SearchAnchorSource anchorSource)
        {
            if (request.CurrentLatitude.HasValue && request.CurrentLongitude.HasValue)
            {
                searchRadiusMeters = NearbySearchRadiusMeters;
                anchorSource = SearchAnchorSource.CurrentLocation;
                return (request.CurrentLatitude.Value, request.CurrentLongitude.Value);
            }

            var located = schedules.Where(s => s.Latitude.HasValue && s.Longitude.HasValue).ToList();

            if (located.Count > 0)
            {
                searchRadiusMeters = TripAreaSearchRadiusMeters;
                anchorSource = SearchAnchorSource.TripArea;
                return (located.Average(s => s.Latitude!.Value), located.Average(s => s.Longitude!.Value));
            }

            searchRadiusMeters = 0;
            anchorSource = SearchAnchorSource.TripArea;
            return null;
        }

        private static List<ChatHistoryMessage> DeserializeHistory(string messagesJson)
        {
            try
            {
                return JsonSerializer.Deserialize<List<ChatHistoryMessage>>(messagesJson) ?? new();
            }
            catch (JsonException)
            {
                return new List<ChatHistoryMessage>();
            }
        }

        private static List<ChatHistoryMessage> TrimToSlidingWindow(List<ChatHistoryMessage> history)
        {
            if (history.Count <= MaxHistoryMessages)
            {
                return history;
            }

            return history
                .Skip(history.Count - MaxHistoryMessages)
                .ToList();
        }

        private static AiChatResponseDto? TryParseRecommendation(string rawReply)
        {
            try
            {
                return JsonSerializer.Deserialize<AiChatResponseDto>(ExtractJson(rawReply), ResponseJsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // 모델이 지시를 어기고 코드블록으로 감싸는 경우를 대비한 방어적 처리.
        private static string ExtractJson(string rawReply)
        {
            var text = rawReply.Trim();

            if (text.StartsWith("```"))
            {
                var firstNewline = text.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    text = text[(firstNewline + 1)..];
                }

                var closingFence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (closingFence >= 0)
                {
                    text = text[..closingFence];
                }
            }

            return text.Trim();
        }
    }
}
