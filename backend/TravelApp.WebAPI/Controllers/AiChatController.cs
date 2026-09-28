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
using TravelApp.WebAPI.Services.PlaceImage;
using TravelApp.WebAPI.Services.PlaceSearch;
using TravelApp.WebAPI.Services.QueryAnalysis;

namespace TravelApp.WebAPI.Controllers
{
    [Route("api/Trip/{tripId}/[controller]")]
    [ApiController]
    [Authorize]
    public class AiChatController : ControllerBase
    {
        private const int MaxHistoryMessages = 16;

        private readonly ApplicationDbContext _context;
        private readonly AnthropicLlmClient _llmClient;
        private readonly PlaceCandidateSearchService _candidateSearch;
        private readonly IPlaceImageProvider _placeImageProvider;
        private readonly ILogger<AiChatController> _logger;

        // 환각 검토용 로그. 수동 검토 테스트(AiChatHallucinationReview)가 이 EventId로 로그를 골라낸다.
        public static readonly EventId QuestionLogEvent = new(1001, "AiChatQuestion");
        public static readonly EventId RawReplyLogEvent = new(1002, "AiChatRawReply");
        public static readonly EventId GroundingLogEvent = new(1003, "AiChatGrounding");
        public static readonly EventId CandidateSearchLogEvent = new(1004, "AiChatCandidateSearch");
        public static readonly EventId QueryAnalysisLogEvent = new(1005, "AiChatQueryAnalysis");

        public AiChatController(
            ApplicationDbContext context,
            AnthropicLlmClient llmClient,
            INearbyPlaceSearchProvider placeSearchProvider,
            IPlaceImageProvider placeImageProvider,
            ILogger<AiChatController> logger)
        {
            _context = context;
            _llmClient = llmClient;
            _candidateSearch = new PlaceCandidateSearchService(placeSearchProvider, new LocationResolver(placeSearchProvider));
            _placeImageProvider = placeImageProvider;
            _logger = logger;
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
        //  1) 질문 분석(TravelQueryAnalyzer): 의도/위치/카테고리를 서버가 정한다.
        //  2) 후보 검색(PlaceCandidateSearchService): 분석 결과로 검색 기준점을 정하고 그 주변 후보를 모은다.
        //     "강남 맛집"이면 GPS가 와도 강남 기준이다. 기준점을 못 정했거나 후보가 없거나 검색 API가 실패하면
        //     LLM을 부르지 않고 이유를 바로 답한다(부르면 장소명을 추측할 수 있으므로).
        //  3) LLM은 후보 목록 중에서 고르고 설명만 한다.
        //  4) 검증(GroundToCandidates): 후보 목록에 있는 장소(placeId 우선)만 남긴다.
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

            var askedAt = DateTime.UtcNow;
            var analysis = TravelQueryAnalyzer.Analyze(request.Message);

            _logger.LogInformation(QuestionLogEvent,
                "AI 챗봇 질문 (Trip {TripId}, Session {SessionId}): {Question}", tripId, sessionId, request.Message);
            _logger.LogInformation(QueryAnalysisLogEvent,
                "질문 분석 (Session {SessionId}): 의도 {Intent}, 위치 {Location}, 카테고리 {Category}, 메뉴 {MenuKeyword}, 특정 장소 {SpecificPlace}, 상대 위치 {RelativeLocation}",
                sessionId, analysis.Intent, analysis.Location ?? "-", analysis.Category, analysis.MenuKeyword ?? "-", analysis.SpecificPlace ?? "-", analysis.RelativeLocation);

            var schedules = await _context.Schedules
                .Where(s => s.TripId == tripId)
                .OrderBy(s => s.StartTime)
                .ThenBy(s => s.Order)
                .ToListAsync();

            var search = analysis.Intent == TravelQueryIntent.NonRecommendation
                ? new CandidateSearchResult(ChatSearchStatus.NotRecommendation, null, 0, [], null)
                : await _candidateSearch.SearchAsync(
                    analysis, request.CurrentLatitude, request.CurrentLongitude, schedules, askedAt, HttpContext.RequestAborted);

            _logger.LogInformation(CandidateSearchLogEvent,
                "주변 후보 검색 (Session {SessionId}): 상태 {SearchStatus}, 중심점 {AnchorSource} '{AnchorLabel}', 반경 {RadiusMeters}m, 후보 {CandidateCount}건, 일정 중복 제외 {ExcludedAsScheduled}건",
                sessionId, search.Status, search.Anchor?.Source.ToString() ?? "없음", search.Anchor?.Label ?? "-", search.RadiusMeters, search.Candidates.Count, search.ExcludedAsScheduled);

            if (search.Status == ChatSearchStatus.SearchUnavailable)
            {
                _logger.LogWarning("장소 검색 API 호출 실패 (Session {SessionId}): {Error}", sessionId, search.ErrorDetail);
            }

            var history = DeserializeHistory(session.MessagesJson);
            history.Add(new ChatHistoryMessage { Role = "user", Content = request.Message });

            if (search.Status is not (ChatSearchStatus.Success or ChatSearchStatus.NotRecommendation))
            {
                var failure = new AiChatResponseDto
                {
                    ReplyText = search.UserMessage ?? string.Empty,
                    SearchStatus = search.Status,
                    SearchLocation = search.Anchor?.Label
                };

                // 대화 이력에도 LLM 응답과 같은 JSON 형태로 남겨, 다음 턴의 LLM이 형식을 헷갈리지 않게 한다.
                await SaveHistoryAsync(session, history, JsonSerializer.Serialize(new { replyText = failure.ReplyText, recommendations = Array.Empty<object>() }));
                return Ok(failure);
            }

            var systemPrompt = AiChatPromptBuilder.BuildSystemPrompt(new ChatPromptContext(
                trip, schedules, request.Message, analysis, search.Anchor, search.RadiusMeters, search.Candidates, askedAt,
                search.ExcludedAsScheduled));

            string rawReply;
            try
            {
                rawReply = await _llmClient.SendAsync(systemPrompt, TrimToSlidingWindow(history), HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
            {
                _logger.LogWarning("LLM 호출 실패 (Session {SessionId}): {Error}", sessionId, ex.Message);
                return StatusCode(502, $"AI 응답을 가져오지 못했습니다: {ex.Message}");
            }

            _logger.LogInformation(RawReplyLogEvent, "LLM 원본 응답 (Session {SessionId}): {RawReply}", sessionId, rawReply);

            var reply = AiChatReplyParser.TryParse(rawReply);

            if (reply == null)
            {
                return StatusCode(502, "AI 응답을 해석하지 못했습니다.");
            }

            var rawRecommendations = AiChatReplyParser.ToRecommendations(reply, trip.StartDate);
            var response = new AiChatResponseDto
            {
                ReplyText = reply.ReplyText,
                SearchStatus = search.Status,
                SearchLocation = search.Anchor?.Label
            };

            if (analysis.Intent == TravelQueryIntent.NonRecommendation)
            {
                // 추천 요청이 아니었으므로 LLM이 장소를 넣었더라도 보여주지 않는다.
                foreach (var dropped in rawRecommendations)
                {
                    _logger.LogInformation(GroundingLogEvent, "추천 {PlaceName}: {Verdict} ({Reason})",
                        dropped.PlaceName, "제외", "장소 추천 요청이 아님");
                }
            }
            else
            {
                var outcomes = PlaceRecommendationGrounder.GroundToCandidates(rawRecommendations, search.Candidates, askedAt);

                foreach (var outcome in outcomes)
                {
                    _logger.LogInformation(GroundingLogEvent,
                        "추천 {PlaceName}: {Verdict} ({Reason})",
                        outcome.Recommendation.PlaceName, outcome.Accepted ? "채택" : "제외", outcome.Reason);
                }

                response.Recommendations = outcomes.Where(o => o.Accepted).Select(o => o.Recommendation).ToList();
                await AttachImagesAsync(sessionId, response.Recommendations, HttpContext.RequestAborted);

                // LLM이 추천을 냈지만 하나도 검증되지 않았다 — LLM 실패(502)와 구분해 검증 실패로 알린다.
                if (rawRecommendations.Count > 0 && response.Recommendations.Count == 0)
                {
                    response.SearchStatus = ChatSearchStatus.GroundingFailed;
                    response.ReplyText += " (추천한 장소를 이번 검색 결과에서 확인하지 못해 보여드리지 않았어요.)";
                }
            }

            await SaveHistoryAsync(session, history, rawReply);
            return Ok(response);
        }

        // 검증을 통과한 추천에만 대표 사진 URL을 붙인다. 사진 조회 실패는 추천 자체를 막지 않는다(ImageUrl=null -> 클라이언트 플레이스홀더).
        private async Task AttachImagesAsync(
            int sessionId, IReadOnlyList<AiPlaceRecommendationDto> recommendations, CancellationToken cancellationToken)
        {
            await Task.WhenAll(recommendations.Select(async recommendation =>
            {
                try
                {
                    recommendation.ImageUrl = await _placeImageProvider.GetRepresentativeImageUrlAsync(
                        recommendation.PlaceName, recommendation.Latitude, recommendation.Longitude, cancellationToken);
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException ||
                                           (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
                {
                    _logger.LogWarning("장소 사진 조회 실패 (Session {SessionId}, {PlaceName}): {Error}",
                        sessionId, recommendation.PlaceName, ex.Message);
                    recommendation.ImageUrl = null;
                }
            }));
        }

        private async Task SaveHistoryAsync(ChatSession session, List<ChatHistoryMessage> history, string assistantContent)
        {
            history.Add(new ChatHistoryMessage { Role = "assistant", Content = assistantContent });
            session.MessagesJson = JsonSerializer.Serialize(TrimToSlidingWindow(history));
            await _context.SaveChangesAsync();
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
    }
}
