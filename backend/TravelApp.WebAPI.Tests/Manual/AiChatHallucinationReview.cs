using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedData.DTOs;
using SharedData.Models;
using TravelApp.WebAPI.Controllers;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services.Llm;
using TravelApp.WebAPI.Services.PlaceImage;
using TravelApp.WebAPI.Services.PlaceSearch;
using Xunit;
using Xunit.Abstractions;

namespace TravelApp.WebAPI.Tests.Manual
{
    // 챗봇 환각 수동 검토용. CI에서 도는 일반 유닛테스트가 아니다 — 실제 Anthropic API와 실제 Tmap API를
    // 호출하므로(비용 발생, 응답이 매번 다름) 사람이 출력을 눈으로 보고 판단한다. 아무것도 assert하지 않는다.
    //
    // 실행 방법 (backend 폴더에서):
    //   dotnet test TravelApp.WebAPI.Tests --filter Category=Manual --logger "console;verbosity=detailed"
    //
    // - 일반 `dotnet test`에서는 csproj의 기본 필터(Category!=Manual)로 빠진다. --filter를 주면 그 필터가 대신 쓰인다.
    // - xUnit은 Console.WriteLine을 버리므로 ITestOutputHelper로 출력한다. verbosity=detailed여야 콘솔에 보인다.
    // - API 키는 WebAPI 프로젝트의 user-secrets(Anthropic:ApiKey, Tmap:AppKey)에서 읽는다.
    // - 웹 서버가 실행 중이라 bin 폴더가 잠겨 빌드가 안 되면 --artifacts-path <임시폴더>를 붙인다.
    // - 특정 케이스만 돌리려면 환경변수 MANUAL_CASES에 케이스 번호를 쉼표로 준다(반복 가능, 예: MANUAL_CASES=9,9,9).
    // - 앱은 위치 권한을 허락하면 모든 메시지에 GPS를 보낸다. 그 상황을 재현하려면 MANUAL_ALWAYS_GPS=1 (모든 케이스에 광화문 GPS 포함).
    [Trait("Category", "Manual")]
    public class AiChatHallucinationReview(ITestOutputHelper output)
    {
        private const string WebApiUserSecretsId = "fda478a2-971c-4485-b517-7b57819a0aae";
        private const int TripId = 1;
        private const int UserId = 1;

        // 광화문 광장 부근. 9번 케이스("이 근처")의 현재 위치로 쓴다.
        private const double GwanghwamunLatitude = 37.5759;
        private const double GwanghwamunLongitude = 126.9769;

        private static bool AlwaysSendGps => Environment.GetEnvironmentVariable("MANUAL_ALWAYS_GPS") == "1";

        // (질문, GPS 좌표 포함 여부). 장소 언급 추천 / 회피 패턴 / 오탐 방지 / 회귀 확인 케이스.
        private static readonly (string Question, bool WithGps)[] Cases =
        [
            ("광화문에서 점심 먹을만한 곳 추천해줘", false),
            ("강남에서 파스타 먹고 싶어", false),
            ("홍대 근처에서 술 한잔할 데 있을까", false),
            ("경복궁 주변 카페 추천", false),
            ("부산 자갈치시장 근처에 뭐 먹을 거 있어?", false),
            ("제주도 성산일출봉 근처 맛집", false),
            ("광화문에 있는 몽로 어때? 거기 기준으로 다른 곳도 추천해줘", false),
            ("우육면당 광화문점 가는 길에 들를만한 카페 있어?", false),
            ("이 근처에 뭐 볼만한 거 있어?", true),
            ("추천해줘", false),
        ];

        [Fact]
        public async Task 열개_케이스의_질문_LLM원본응답_그라운딩결과를_출력한다()
        {
            var configuration = LoadWebApiConfiguration();
            using var llmHttp = new HttpClient();
            using var tmapHttp = new HttpClient();
            var llmClient = new AnthropicLlmClient(llmHttp, configuration);
            var placeSearchProvider = new TmapNearbyPlaceSearchProvider(tmapHttp, configuration);

            using var db = CreateSeededDb();
            var logger = new CapturingLogger();
            var controller = new AiChatController(db, llmClient, placeSearchProvider, new MockPlaceImageProvider(), logger)
            {
                ControllerContext = new ControllerContext { HttpContext = CreateHttpContext() }
            };

            foreach (int i in SelectedCaseIndexes())
            {
                var (question, caseHasGps) = Cases[i];
                bool withGps = caseHasGps || AlwaysSendGps;
                logger.Entries.Clear();

                output.WriteLine($"[케이스 {i + 1}] 질문: {question}{(withGps ? $" (GPS {GwanghwamunLatitude}, {GwanghwamunLongitude})" : "")}");

                try
                {
                    // 케이스마다 새 세션: 앞 케이스의 대화 이력이 섞이지 않게 한다.
                    var session = (ChatSessionCreateResponseDto)((OkObjectResult)(await controller.CreateSession(TripId)).Result!).Value!;

                    var result = await controller.SendMessage(TripId, session.SessionId, new AiChatRequestDto
                    {
                        Message = question,
                        CurrentLatitude = withGps ? GwanghwamunLatitude : null,
                        CurrentLongitude = withGps ? GwanghwamunLongitude : null
                    });

                    WriteCaseResult(logger, result);
                }
                catch (Exception ex)
                {
                    output.WriteLine($"실행 오류: {ex.GetType().Name}: {ex.Message}");
                }

                output.WriteLine("");
            }
        }

        private static IEnumerable<int> SelectedCaseIndexes()
        {
            string? selected = Environment.GetEnvironmentVariable("MANUAL_CASES");

            if (string.IsNullOrWhiteSpace(selected))
            {
                return Enumerable.Range(0, Cases.Length);
            }

            return selected
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(n => int.Parse(n) - 1);
        }

        private void WriteCaseResult(CapturingLogger logger, ActionResult<AiChatResponseDto> result)
        {
            var search = logger.Entries.LastOrDefault(e => e.EventId == AiChatController.CandidateSearchLogEvent);
            if (search != null)
            {
                output.WriteLine($"주변 후보 검색: 상태 {search.Get("SearchStatus")}, 중심점 {search.Get("AnchorSource")} '{search.Get("AnchorLabel")}', 반경 {search.Get("RadiusMeters")}m, 후보 {search.Get("CandidateCount")}건, 일정 중복 제외 {search.Get("ExcludedAsScheduled")}건");
            }

            var analysis = logger.Entries.LastOrDefault(e => e.EventId == AiChatController.QueryAnalysisLogEvent);
            if (analysis != null)
            {
                output.WriteLine($"질문 분석: 의도 {analysis.Get("Intent")}, 위치 {analysis.Get("Location")}, 카테고리 {analysis.Get("Category")}, 메뉴 {analysis.Get("MenuKeyword")}, 특정 장소 {analysis.Get("SpecificPlace")}");
            }

            foreach (var warning in logger.Entries.Where(e => e.Level >= LogLevel.Warning))
            {
                output.WriteLine($"경고 로그: {warning.Message}");
            }

            output.WriteLine($"LLM 원본 응답: {logger.Value(AiChatController.RawReplyLogEvent, "RawReply") ?? "(없음)"}");

            var grounding = logger.Entries.Where(e => e.EventId == AiChatController.GroundingLogEvent).ToList();
            var accepted = grounding.Where(e => (string?)e.Get("Verdict") == "채택").Select(e => $"{e.Get("PlaceName")} ({e.Get("Reason")})");
            var rejected = grounding.Where(e => (string?)e.Get("Verdict") == "제외").Select(e => $"{e.Get("PlaceName")} — {e.Get("Reason")}");

            output.WriteLine($"최종 추천(그라운딩 통과): [{string.Join(", ", accepted)}]");
            output.WriteLine($"제외된 추천과 사유: [{string.Join(", ", rejected)}]");

            switch (result.Result)
            {
                case OkObjectResult { Value: AiChatResponseDto response }:
                    output.WriteLine($"사용자에게 보인 답변: {response.ReplyText} [상태 {response.SearchStatus}, 기준 {response.SearchLocation ?? "-"}]");
                    break;
                case ObjectResult other:
                    output.WriteLine($"응답 상태 {other.StatusCode}: {other.Value}");
                    break;
                default:
                    output.WriteLine($"응답: {result.Result?.GetType().Name}");
                    break;
            }
        }

        // 서울 여행 하나. 좌표가 있는 일정 2개가 있어서, GPS가 없는 케이스는 이 일정들의 무게중심(광화문 일대)이
        // 주변 후보 검색의 중심점이 된다(10번 "일정 있는 여행" 케이스의 전제).
        private static ApplicationDbContext CreateSeededDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new ApplicationDbContext(options);

            var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            db.Trips.Add(new Trip { Id = TripId, Title = "서울 여행", OwnerId = UserId, StartDate = start, EndDate = start.AddDays(2) });
            db.TripMembers.Add(new TripMember { TripId = TripId, UserId = UserId });
            db.Schedules.Add(new Schedule
            {
                TripId = TripId, Title = "경복궁 관람", PlaceName = "경복궁",
                StartTime = start.AddHours(10), EndTime = start.AddHours(12),
                Latitude = 37.5796, Longitude = 126.9770
            });
            db.Schedules.Add(new Schedule
            {
                TripId = TripId, Title = "광화문광장 산책", PlaceName = "광화문광장",
                StartTime = start.AddHours(13), EndTime = start.AddHours(14),
                Latitude = 37.5725, Longitude = 126.9769
            });
            db.SaveChanges();
            return db;
        }

        private static DefaultHttpContext CreateHttpContext() => new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, UserId.ToString()) }, "manual"))
        };

        private static IConfiguration LoadWebApiConfiguration([CallerFilePath] string thisFile = "")
        {
            string webApiDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "TravelApp.WebAPI"));

            return new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(webApiDir, "appsettings.json"), optional: false)
                .AddUserSecrets(WebApiUserSecretsId)
                .Build();
        }

        // 컨트롤러가 남기는 구조화 로그를 그대로 모은다(메시지 문자열을 파싱하지 않고 이름 붙은 값으로 꺼낸다).
        private sealed class CapturingLogger : ILogger<AiChatController>
        {
            public List<LogEntry> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var values = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
                Entries.Add(new LogEntry(eventId, values.ToDictionary(kv => kv.Key, kv => kv.Value), logLevel, formatter(state, exception)));
            }

            public string? Value(EventId eventId, string key) =>
                Entries.LastOrDefault(e => e.EventId == eventId)?.Get(key) as string;
        }

        private sealed record LogEntry(EventId EventId, Dictionary<string, object?> Values, LogLevel Level, string Message)
        {
            public object? Get(string key) => Values.GetValueOrDefault(key);
        }
    }
}
