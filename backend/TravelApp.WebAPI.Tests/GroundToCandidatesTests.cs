using SharedData.DTOs;
using SharedData.Models;
using TravelApp.WebAPI.Services;
using TravelApp.WebAPI.Services.PlaceSearch;
using TravelApp.WebAPI.Services.QueryAnalysis;
using Xunit;
using static TravelApp.WebAPI.Tests.FakeNameSearchProvider;

namespace TravelApp.WebAPI.Tests
{
    // AI 챗봇의 검증: 후보 목록 안에서만, placeId를 우선으로 확인한다.
    public class GroundToCandidatesTests
    {
        private static readonly DateTime Now = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        private static List<PlaceSearchResultDto> StarbucksBranches() =>
        [
            Place("스타벅스 강남역점", latitude: 37.4981, longitude: 127.0277),
            Place("스타벅스 역삼점", latitude: 37.5000, longitude: 127.0360),
            Place("스타벅스 삼성점", latitude: 37.5088, longitude: 127.0630),
        ];

        private static AiPlaceRecommendationDto Rec(string name, string? placeId = null) =>
            new() { PlaceName = name, PlaceId = placeId };

        private static GroundingOutcome GroundOne(AiPlaceRecommendationDto recommendation, List<PlaceSearchResultDto> candidates) =>
            Assert.Single(PlaceRecommendationGrounder.GroundToCandidates([recommendation], candidates, Now));

        [Fact]
        public void 후보에_없는_장소명은_제거된다()
        {
            var outcome = GroundOne(Rec("광화문 숨은 파스타집"), StarbucksBranches());

            Assert.False(outcome.Accepted);
            Assert.Equal(GroundingStage.NotInCandidates, outcome.Stage);
            Assert.Null(outcome.Recommendation.Latitude);
        }

        [Fact]
        public void 후보에_없는_placeId는_이름이_맞아도_제거된다()
        {
            var outcome = GroundOne(Rec("스타벅스 역삼점", placeId: "c99"), StarbucksBranches());

            Assert.False(outcome.Accepted);
            Assert.Equal(GroundingStage.UnknownPlaceId, outcome.Stage);
        }

        [Fact]
        public void placeId로_지점을_정확히_유지하고_실제_PlaceId와_좌표를_붙인다()
        {
            var outcome = GroundOne(Rec("스타벅스 역삼점", placeId: "c2"), StarbucksBranches());

            Assert.True(outcome.Accepted);
            Assert.Equal(GroundingStage.PlaceIdMatched, outcome.Stage);
            Assert.Equal("id:스타벅스 역삼점", outcome.Recommendation.PlaceId);
            Assert.Equal("스타벅스 역삼점", outcome.Recommendation.PlaceName);
            Assert.Equal(37.5000, outcome.Recommendation.Latitude);
        }

        [Fact]
        public void placeId와_이름이_서로_다른_지점이면_지점_혼동으로_제거된다()
        {
            // c1은 강남역점인데 이름은 역삼점 — 어느 쪽이 맞는지 알 수 없으므로 보여주지 않는다.
            var outcome = GroundOne(Rec("스타벅스 역삼점", placeId: "c1"), StarbucksBranches());

            Assert.False(outcome.Accepted);
            Assert.Equal(GroundingStage.PlaceIdNameMismatch, outcome.Stage);
        }

        [Fact]
        public void placeId_없이_지점까지_정확한_이름이면_그_지점으로_채택된다()
        {
            var outcome = GroundOne(Rec("스타벅스 삼성점"), StarbucksBranches());

            Assert.True(outcome.Accepted);
            Assert.Equal("id:스타벅스 삼성점", outcome.Recommendation.PlaceId);
        }

        [Fact]
        public void 브랜드명만_있고_지점이_여럿이면_특정할_수_없어_제거된다()
        {
            var outcome = GroundOne(Rec("스타벅스"), StarbucksBranches());

            Assert.False(outcome.Accepted);
            Assert.Equal(GroundingStage.AmbiguousName, outcome.Stage);
        }

        [Theory]
        [InlineData("ABC", "ABC마트")]
        [InlineData("광화문", "우육면당 광화문점")]
        [InlineData("몽로", "몽로주점")]
        [InlineData("몽로", "청담몽로")]
        [InlineData("서촌(북촌한옥마을)", "북촌한옥마을")]
        public void 짧은_이름의_부분일치는_같은_장소로_보지_않는다(string recommended, string candidate)
        {
            Assert.False(PlaceRecommendationGrounder.IsSufficientlySimilar(recommended, candidate));
            Assert.False(GroundOne(Rec(recommended), [Place(candidate)]).Accepted);
        }

        [Theory]
        [InlineData("몽로", "몽로 광화문점")]
        [InlineData("명동교자", "명동교자 본점")]
        [InlineData("우육면관 청계천점", "우육면관 청계천점[중식]")]
        [InlineData("에스프레소 살롱", "에스프레소쌀롱")]
        public void 지점명만_붙었거나_표기만_다르면_같은_장소로_본다(string recommended, string candidate)
        {
            Assert.True(PlaceRecommendationGrounder.IsSufficientlySimilar(recommended, candidate));
        }

        [Fact]
        public void 영업정보가_없는_후보는_추천되지만_Open으로_표시되지_않는다()
        {
            var unknown = Place("흥례문");
            unknown.OperatingStatus = PlaceOperatingStatus.Unknown;

            var outcome = GroundOne(Rec("흥례문", placeId: "c1"), [unknown]);

            Assert.True(outcome.Accepted);
            Assert.Equal(PlaceOperatingStatus.Unknown, outcome.Recommendation.OperatingStatus);
            Assert.NotEqual(PlaceOperatingStatus.Open, outcome.Recommendation.OperatingStatus);
        }

        [Fact]
        public void 최근_영업이_확인된_후보만_Open으로_표시된다()
        {
            var confirmed = Place("계절밥상 본점", lastConfirmed: Now.AddDays(-10));

            var outcome = GroundOne(Rec("계절밥상 본점"), [confirmed]);

            Assert.Equal(PlaceOperatingStatus.Open, outcome.Recommendation.OperatingStatus);
        }

        [Fact]
        public void 폐업한_후보는_placeId가_맞아도_제거된다()
        {
            var closed = Place("추억의 분식집", closed: true);

            var outcome = GroundOne(Rec("추억의 분식집", placeId: "c1"), [closed]);

            Assert.False(outcome.Accepted);
            Assert.Equal(GroundingStage.NotOperating, outcome.Stage);
        }

        [Fact]
        public void 프롬프트는_영업정보_없는_후보를_확인_안_됨으로_표시하고_핵심_원칙을_담는다()
        {
            var trip = new Trip { Title = "서울 여행", StartDate = Now, EndDate = Now.AddDays(2) };
            var analysis = TravelQueryAnalyzer.Analyze("강남 카페");
            var context = new ChatPromptContext(trip, [], "강남 카페", analysis,
                new SearchAnchor(37.4979, 127.0276, "강남역", SearchAnchorSource.NamedLocation), 1500, StarbucksBranches(), Now);

            string prompt = AiChatPromptBuilder.BuildSystemPrompt(context);

            Assert.Contains("placeId: c1 | 이름: 스타벅스 강남역점", prompt);
            Assert.Contains("영업 상태: 확인 안 됨", prompt);
            Assert.DoesNotContain("영업 중", prompt.Split("[추천 규칙]")[0]);
            Assert.Contains("추천 장소는 반드시 제공된 후보 목록에 존재해야 한다", prompt);
            Assert.Contains("후보 목록에 없는 장소를 생성하지 않는다", prompt);
            Assert.Contains("후보가 없으면 장소명을 추측하지 않는다", prompt);
            Assert.Contains("검색 결과가 부족하면 추천하지 않고 부족한 이유를 반환한다", prompt);
            Assert.Contains("검색 의도: 사용자가 말한 지역 기준 검색", prompt);
            Assert.Contains("검색 기준점: 강남역", prompt);
        }

        [Fact]
        public void 프롬프트는_일정_재추천_금지와_추천끼리_시간_겹침_금지와_이유문장_주의를_담는다()
        {
            var trip = new Trip { Title = "서울 여행", StartDate = Now, EndDate = Now.AddDays(2) };
            var analysis = TravelQueryAnalyzer.Analyze("강남에서 파스타 먹고 싶어");
            var context = new ChatPromptContext(trip, [], "강남에서 파스타 먹고 싶어", analysis,
                new SearchAnchor(37.4979, 127.0276, "강남역", SearchAnchorSource.NamedLocation), 2000, StarbucksBranches(), Now,
                ExcludedAsScheduled: 2);

            string prompt = AiChatPromptBuilder.BuildSystemPrompt(context);

            Assert.Contains("이미 계획된 일정에 있는 장소와 같은 곳은 다시 추천하지 않는다", prompt);
            Assert.Contains("추천한 장소끼리 suggestedStartTime~suggestedEndTime이 서로 겹치지 않게", prompt);
            Assert.Contains("구체적 사실 주장(특정 메뉴, 유명세, 요리법", prompt);
            Assert.Contains("찾는 메뉴: 파스타", prompt);
            Assert.Contains("이미 일정에 있는 장소 2곳은 후보에서 제외했습니다", prompt);
        }

        // 아래 규칙들의 실제 판단 품질은 Manual 케이스(1번, 9번)로 확인한다. 여기서는 추천 성격을 서버가 정해 주는 부분과
        // 예외 조항 문구가 프롬프트에 들어가는지만 확인한다.
        [Theory]
        [InlineData("광화문에서 점심 먹을만한 곳 추천해줘")]
        [InlineData("경복궁 주변 카페 추천")]
        [InlineData("홍대 근처에서 술 한잔할 데 있을까")]
        public void 식당_카페_술집_추천은_같은_목적의_대안으로_표시된다(string message)
        {
            string prompt = BuildPrompt(message);

            Assert.Contains("- 추천 성격: 같은 목적의 대안", prompt);
            Assert.Contains("같은 시간대나 비슷한 시간대를 제안하는 것이 자연스럽다", prompt);
        }

        [Theory]
        [InlineData("이 근처에 뭐 볼만한 거 있어?")]
        [InlineData("추천해줘")]
        public void 관광지나_일반_추천은_서로_다른_활동의_일정_묶음으로_표시된다(string message)
        {
            string prompt = BuildPrompt(message);

            Assert.Contains("- 추천 성격: 서로 다른 활동의 일정 묶음", prompt);
            Assert.Contains("서로 다른 관광지·명소는 대안이 아니라 각각 따로 방문하는 활동이다", prompt);
        }

        [Fact]
        public void 프롬프트는_느슨한_기존_일정과는_겹쳐도_되고_고정된_일정과는_겹치지_말라고_알린다()
        {
            string prompt = BuildPrompt("이 근처에 뭐 볼만한 거 있어?");

            Assert.Contains("\"산책\", \"자유시간\", \"휴식\", \"쇼핑\", \"이동\"", prompt);
            Assert.Contains("느슨하게 잡힌 기존 일정의 시간대와는 겹쳐도 된다", prompt);
            Assert.Contains("\"경복궁 관람\"처럼 특정 장소나 활동에 고정된 일정과는 시간이 겹치지 않게 한다", prompt);
            Assert.Contains("\"그 일정 중에 볼 수 있다\", \"관람의 일부로 체험할 수 있다\" 같은 이유로 그 일정 시간 안에 넣지 않는다", prompt);
        }

        private static string BuildPrompt(string message)
        {
            var trip = new Trip { Title = "서울 여행", StartDate = Now, EndDate = Now.AddDays(2) };
            var schedules = new List<Schedule>
            {
                new() { Title = "경복궁 관람", PlaceName = "경복궁", StartTime = Now.AddHours(10), EndTime = Now.AddHours(12) },
                new() { Title = "광화문광장 산책", PlaceName = "광화문광장", StartTime = Now.AddHours(13), EndTime = Now.AddHours(14) },
            };

            return AiChatPromptBuilder.BuildSystemPrompt(new ChatPromptContext(trip, schedules, message, TravelQueryAnalyzer.Analyze(message),
                new SearchAnchor(37.5759, 126.9769, "광화문", SearchAnchorSource.NamedLocation), 2000, StarbucksBranches(), Now));
        }
    }
}
