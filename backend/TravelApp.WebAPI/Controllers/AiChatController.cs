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

namespace TravelApp.WebAPI.Controllers
{
    [Route("api/Trip/{tripId}/[controller]")]
    [ApiController]
    [Authorize]
    public class AiChatController : ControllerBase
    {
        private const int MaxHistoryMessages = 16;

        private static readonly JsonSerializerOptions ResponseJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ApplicationDbContext _context;
        private readonly AnthropicLlmClient _llmClient;

        public AiChatController(ApplicationDbContext context, AnthropicLlmClient llmClient)
        {
            _context = context;
            _llmClient = llmClient;
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

            var history = DeserializeHistory(session.MessagesJson);
            history.Add(new ChatHistoryMessage { Role = "user", Content = request.Message });
            history = TrimToSlidingWindow(history);

            var systemPrompt = AiChatPromptBuilder.BuildSystemPrompt(trip, schedules);

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

            history.Add(new ChatHistoryMessage { Role = "assistant", Content = rawReply });
            history = TrimToSlidingWindow(history);

            session.MessagesJson = JsonSerializer.Serialize(history);
            await _context.SaveChangesAsync();

            return Ok(parsedResponse);
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
