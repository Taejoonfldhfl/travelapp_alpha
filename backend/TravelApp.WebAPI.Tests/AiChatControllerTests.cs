using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SharedData.DTOs;
using SharedData.Models;
using TravelApp.WebAPI.Controllers;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services.Llm;
using TravelApp.WebAPI.Services.PlaceImage;
using Xunit;
using static TravelApp.WebAPI.Tests.FakeNameSearchProvider;

namespace TravelApp.WebAPI.Tests
{
    // SendMessage 전체 흐름. Anthropic 호출은 HttpClient를 가짜 응답으로 바꿔 실제 API 없이 검증한다.
    public class AiChatControllerTests
    {
        private const int TripId = 1;
        private const int UserId = 1;

        // 장소 이름별로 정해 둔 사진 URL을 돌려주고, 어떤 추천(이름/좌표)으로 호출됐는지 기록한다. Throw를 주면 조회 실패를 재현한다.
        private sealed class FakePlaceImageProvider(Dictionary<string, string> urlsByPlaceName) : IPlaceImageProvider
        {
            public List<(string PlaceName, double? Latitude, double? Longitude)> Calls { get; } = new();

            public Exception? Throw { get; init; }

            public Task<string?> GetRepresentativeImageUrlAsync(
                string placeName, double? latitude, double? longitude, CancellationToken cancellationToken = default)
            {
                lock (Calls)
                {
                    Calls.Add((placeName, latitude, longitude));
                }

                if (Throw != null)
                {
                    throw Throw;
                }

                return Task.FromResult(urlsByPlaceName.GetValueOrDefault(placeName));
            }
        }

        // Anthropic Messages API 응답을 흉내 낸다. 호출 횟수와 마지막 시스템 프롬프트를 기록한다.
        private sealed class StubLlmHandler(string replyJson) : HttpMessageHandler
        {
            public int CallCount { get; private set; }
            public string? LastRequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;
                LastRequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

                string body = JsonSerializer.Serialize(new { content = new[] { new { type = "text", text = replyJson } } });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            }
        }

        private static async Task<(AiChatController Controller, StubLlmHandler Llm, int SessionId)> CreateAsync(
            FakeNameSearchProvider provider, string llmReplyJson, IPlaceImageProvider? imageProvider = null)
        {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            db.Trips.Add(new Trip { Id = TripId, Title = "서울 여행", OwnerId = UserId, StartDate = start, EndDate = start.AddDays(2) });
            db.TripMembers.Add(new TripMember { TripId = TripId, UserId = UserId });
            db.SaveChanges();

            var llm = new StubLlmHandler(llmReplyJson);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Anthropic:ApiKey"] = "test-key" })
                .Build();

            var controller = new AiChatController(db, new AnthropicLlmClient(new HttpClient(llm), configuration), provider, imageProvider ?? new MockPlaceImageProvider(),
                NullLogger<AiChatController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, UserId.ToString()) }, "test"))
                    }
                }
            };

            var session = (ChatSessionCreateResponseDto)((OkObjectResult)(await controller.CreateSession(TripId)).Result!).Value!;
            return (controller, llm, session.SessionId);
        }

        private static FakeNameSearchProvider GangnamProvider() => new(new()
        {
            ["강남역"] = [Place("강남역", latitude: 37.4979, longitude: 127.0276, category: "교통편의 > 지하철")]
        });

        private static async Task<AiChatResponseDto> Send(AiChatController controller, int sessionId, string message, bool withGps = true)
        {
            var result = await controller.SendMessage(TripId, sessionId, new AiChatRequestDto
            {
                Message = message,
                CurrentLatitude = withGps ? 37.5759 : null,
                CurrentLongitude = withGps ? 126.9769 : null
            });

            return (AiChatResponseDto)Assert.IsType<OkObjectResult>(result.Result).Value!;
        }

        [Fact]
        public async Task 검색결과가_0개면_LLM을_부르지_않고_검색실패_이유를_답한다()
        {
            var provider = GangnamProvider();
            var (controller, llm, sessionId) = await CreateAsync(provider, "{}");

            var response = await Send(controller, sessionId, "강남 맛집");

            Assert.Equal(0, llm.CallCount);
            Assert.Equal(ChatSearchStatus.NoCandidates, response.SearchStatus);
            Assert.Equal("강남 지역에서 조건에 맞는 음식점을 찾지 못했어요.", response.ReplyText);
            Assert.Empty(response.Recommendations);
        }

        [Fact]
        public async Task 존재하지_않는_장소를_물으면_LLM을_부르지_않고_임의_추천도_하지_않는다()
        {
            var (controller, llm, sessionId) = await CreateAsync(new FakeNameSearchProvider(new()), "{}");

            var response = await Send(controller, sessionId, "이세상에없는가게 강남점 알려줘");

            Assert.Equal(0, llm.CallCount);
            Assert.Equal(ChatSearchStatus.LocationNotResolved, response.SearchStatus);
            Assert.StartsWith("말씀하신 장소", response.ReplyText);
            Assert.Empty(response.Recommendations);
        }

        [Fact]
        public async Task LLM이_후보에_없는_장소를_내면_제거하고_검증실패로_알린다()
        {
            var provider = GangnamProvider();
            provider.NearbyResults = [Place("진짜 식당", latitude: 37.4985, longitude: 127.0280)];
            const string reply = """
                {"replyText":"추천드려요","searchStatus":"success","recommendations":[
                  {"placeId":"c7","name":"지어낸 식당","reason":"맛있어요","suggestedStartTime":"2026-10-01T03:00:00Z","suggestedEndTime":"2026-10-01T04:00:00Z"},
                  {"name":"또 지어낸 식당","reason":"좋아요","suggestedStartTime":"2026-10-01T05:00:00Z","suggestedEndTime":"2026-10-01T06:00:00Z"}]}
                """;
            var (controller, llm, sessionId) = await CreateAsync(provider, reply);

            var response = await Send(controller, sessionId, "강남 맛집");

            Assert.Equal(1, llm.CallCount);
            Assert.Empty(response.Recommendations);
            Assert.Equal(ChatSearchStatus.GroundingFailed, response.SearchStatus);
        }

        // ---- 추천 카드 대표 사진(ImageUrl) ----

        private const string TwoCandidatesReply = """
            {"replyText":"추천드려요","searchStatus":"success","recommendations":[
              {"placeId":"c1","name":"진짜 식당","reason":"가까워요","suggestedStartTime":"2026-10-01T03:00:00Z","suggestedEndTime":"2026-10-01T04:00:00Z"},
              {"placeId":"c2","name":"두번째 식당","reason":"좋아요","suggestedStartTime":"2026-10-01T05:00:00Z","suggestedEndTime":"2026-10-01T06:00:00Z"},
              {"name":"지어낸 식당","reason":"맛있어요","suggestedStartTime":"2026-10-01T07:00:00Z","suggestedEndTime":"2026-10-01T08:00:00Z"}]}
            """;

        private static FakeNameSearchProvider TwoCandidatesProvider()
        {
            var provider = GangnamProvider();
            provider.NearbyResults =
            [
                Place("진짜 식당", latitude: 37.4985, longitude: 127.0280),
                Place("두번째 식당", latitude: 37.4990, longitude: 127.0285),
            ];
            return provider;
        }

        [Fact]
        public async Task 검증을_통과한_추천마다_사진_provider로_ImageUrl을_채운다()
        {
            var images = new FakePlaceImageProvider(new() { ["진짜 식당"] = "https://img.example/real.jpg" });
            var (controller, _, sessionId) = await CreateAsync(TwoCandidatesProvider(), TwoCandidatesReply, images);

            var response = await Send(controller, sessionId, "강남 맛집");

            Assert.Equal(2, response.Recommendations.Count);
            Assert.Equal("https://img.example/real.jpg", response.Recommendations.Single(r => r.PlaceName == "진짜 식당").ImageUrl);
            Assert.Null(response.Recommendations.Single(r => r.PlaceName == "두번째 식당").ImageUrl); // 사진 없음 -> null

            // 검증에서 빠진 '지어낸 식당'은 사진을 조회하지 않고, 조회에는 검증된 좌표를 넘긴다.
            Assert.Equal(2, images.Calls.Count);
            Assert.DoesNotContain(images.Calls, c => c.PlaceName == "지어낸 식당");
            Assert.Contains(images.Calls, c => c.PlaceName == "진짜 식당" && c.Latitude == 37.4985 && c.Longitude == 127.0280);
        }

        [Fact]
        public async Task Mock_사진_provider면_추천은_그대로이고_ImageUrl만_null이다()
        {
            var (controller, _, sessionId) = await CreateAsync(TwoCandidatesProvider(), TwoCandidatesReply, new MockPlaceImageProvider());

            var response = await Send(controller, sessionId, "강남 맛집");

            Assert.Equal(2, response.Recommendations.Count);
            Assert.All(response.Recommendations, r => Assert.Null(r.ImageUrl));
        }

        [Fact]
        public async Task 사진_조회가_실패해도_추천은_그대로_돌려주고_ImageUrl만_null이다()
        {
            var images = new FakePlaceImageProvider(new()) { Throw = new HttpRequestException("image api down") };
            var (controller, _, sessionId) = await CreateAsync(TwoCandidatesProvider(), TwoCandidatesReply, images);

            var response = await Send(controller, sessionId, "강남 맛집");

            Assert.Equal(2, response.Recommendations.Count);
            Assert.All(response.Recommendations, r => Assert.Null(r.ImageUrl));
            Assert.Equal(ChatSearchStatus.Success, response.SearchStatus);
        }

        [Fact]
        public async Task 추천이_없으면_사진을_조회하지_않는다()
        {
            var images = new FakePlaceImageProvider(new());
            var (controller, _, sessionId) = await CreateAsync(GangnamProvider(), """{"replyText":"없음","recommendations":[]}""", images);

            await Send(controller, sessionId, "강남 맛집");

            Assert.Empty(images.Calls);
        }

        [Fact]
        public async Task 후보의_placeId로_추천하면_클라이언트_계약대로_좌표와_함께_돌려준다()
        {
            var provider = GangnamProvider();
            provider.NearbyResults = [Place("진짜 식당", latitude: 37.4985, longitude: 127.0280)];
            const string reply = """
                {"replyText":"추천드려요","searchStatus":"success","recommendations":[
                  {"placeId":"c1","name":"진짜 식당","reason":"강남역에서 가까워요","suggestedStartTime":"2026-10-01T03:00:00Z","suggestedEndTime":"2026-10-01T04:00:00Z"}]}
                """;
            var (controller, llm, sessionId) = await CreateAsync(provider, reply);

            var response = await Send(controller, sessionId, "강남 맛집");

            var recommendation = Assert.Single(response.Recommendations);
            Assert.Equal("진짜 식당", recommendation.PlaceName);
            Assert.Equal("강남역에서 가까워요", recommendation.Description);
            Assert.Equal("id:진짜 식당", recommendation.PlaceId);
            Assert.Equal(37.4985, recommendation.Latitude);
            Assert.Equal(PlaceOperatingStatus.Unknown, recommendation.OperatingStatus);
            Assert.Equal(ChatSearchStatus.Success, response.SearchStatus);
            Assert.Equal("강남역", response.SearchLocation);

            // GPS(광화문)가 왔어도 강남역 기준으로 검색했고, 프롬프트에도 그 기준이 들어갔다.
            Assert.Equal(37.4979, provider.NearbyRequests[0].Latitude);
            Assert.Contains("검색 기준점: 강남역", JsonDocument.Parse(llm.LastRequestBody!).RootElement.GetProperty("system").GetString());
        }

        [Fact]
        public async Task 예전_스키마_placeName_description으로_답해도_해석한다()
        {
            var provider = GangnamProvider();
            provider.NearbyResults = [Place("진짜 식당", latitude: 37.4985, longitude: 127.0280)];
            const string reply = """
                {"replyText":"추천","recommendations":[{"placeName":"진짜 식당","description":"설명","suggestedStartTime":"2026-10-01T03:00:00Z","suggestedEndTime":"2026-10-01T04:00:00Z"}]}
                """;
            var (controller, _, sessionId) = await CreateAsync(provider, reply);

            var response = await Send(controller, sessionId, "강남 맛집");

            Assert.Equal("설명", Assert.Single(response.Recommendations).Description);
        }

        [Fact]
        public async Task 추천_요청이_아니면_장소_검색을_하지_않고_LLM이_장소를_넣어도_보여주지_않는다()
        {
            var provider = GangnamProvider();
            const string reply = """
                {"replyText":"오늘 일정은 없어요","searchStatus":"not_recommendation","recommendations":[{"name":"아무데나","reason":"x"}]}
                """;
            var (controller, llm, sessionId) = await CreateAsync(provider, reply);

            var response = await Send(controller, sessionId, "오늘 일정 알려줘");

            Assert.Equal(1, llm.CallCount);
            Assert.Empty(provider.NearbyRequests);
            Assert.Empty(response.Recommendations);
            Assert.Equal(ChatSearchStatus.NotRecommendation, response.SearchStatus);
        }

        [Fact]
        public async Task 검색_API_오류는_검색결과_없음과_다른_상태로_알린다()
        {
            var provider = GangnamProvider();
            provider.ThrowOnSearch = new HttpRequestException("connection refused");
            var (controller, llm, sessionId) = await CreateAsync(provider, "{}");

            var response = await Send(controller, sessionId, "근처 맛집");

            Assert.Equal(0, llm.CallCount);
            Assert.Equal(ChatSearchStatus.SearchUnavailable, response.SearchStatus);
            Assert.Equal("장소 검색 서비스를 일시적으로 사용할 수 없어요. 잠시 후 다시 시도해 주세요.", response.ReplyText);
        }

        [Fact]
        public async Task LLM_응답이_JSON이_아니면_502로_구분한다()
        {
            var provider = GangnamProvider();
            provider.NearbyResults = [Place("진짜 식당", latitude: 37.4985, longitude: 127.0280)];
            var (controller, _, sessionId) = await CreateAsync(provider, "JSON이 아닌 답변");

            var result = await controller.SendMessage(TripId, sessionId, new AiChatRequestDto { Message = "강남 맛집" });

            Assert.Equal(502, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        }
    }
}
