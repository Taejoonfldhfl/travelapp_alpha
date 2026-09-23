using SharedData.DTOs;
using TravelApp.WebAPI.Services;
using Xunit;
using static TravelApp.WebAPI.Tests.FakeNameSearchProvider;

namespace TravelApp.WebAPI.Tests
{
    // GroundAsync(AiChatController가 쓰는 전체 검증 흐름)가 추천마다 어느 단계에서 채택/제외됐는지 정확히 돌려주는지 검증한다.
    // 특히 이름 검색이 추천과 무관한 장소를 돌려줬을 때 유사도 단계에서 걸러지는지 본다.
    // (이름 검색 결과가 여러 건일 때의 순위 순회는 PlaceRecommendationGrounderTests에서 검증한다.)
    public class PlaceRecommendationGrounderAsyncTests
    {
        private static readonly DateTime Now = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        private static List<AiPlaceRecommendationDto> Recommend(params string[] names) =>
            names.Select(n => new AiPlaceRecommendationDto { PlaceName = n }).ToList();

        [Fact]
        public async Task 후보목록과_일치하면_이름검색없이_채택된다()
        {
            var provider = new FakeNameSearchProvider(new());

            var outcomes = await PlaceRecommendationGrounder.GroundAsync(
                Recommend("계절밥상"), [Place("계절밥상 본점")], provider, Now);

            var outcome = Assert.Single(outcomes);
            Assert.True(outcome.Accepted);
            Assert.Equal(GroundingStage.CandidateMatched, outcome.Stage);
            Assert.Equal("id:계절밥상 본점", outcome.Recommendation.PlaceId);
            Assert.Empty(provider.Queries);
        }

        [Fact]
        public async Task 이름검색_결과가_추천과_같은_장소면_채택된다()
        {
            var provider = new FakeNameSearchProvider(new() { ["몽로"] = [Place("몽로 광화문점")] });

            var outcome = Assert.Single(await PlaceRecommendationGrounder.GroundAsync(Recommend("몽로"), [], provider, Now));

            Assert.True(outcome.Accepted);
            Assert.Equal(GroundingStage.NameSearchMatched, outcome.Stage);
            Assert.Equal("id:몽로 광화문점", outcome.Recommendation.PlaceId);
        }

        [Fact]
        public async Task 이름검색이_엉뚱한_장소를_돌려주면_유사도실패로_제외된다()
        {
            var provider = new FakeNameSearchProvider(new() { ["광화문 숨은 파스타집"] = [Place("광화문역 5호선")] });

            var outcome = Assert.Single(await PlaceRecommendationGrounder.GroundAsync(Recommend("광화문 숨은 파스타집"), [], provider, Now));

            Assert.False(outcome.Accepted);
            Assert.Equal(GroundingStage.NameSimilarityFailed, outcome.Stage);
            Assert.Null(outcome.Recommendation.PlaceId);
            Assert.Contains("유사도 실패", outcome.Reason);
            Assert.Contains("광화문역 5호선", outcome.Reason);
        }

        [Fact]
        public async Task 이름검색에서_못찾으면_이름검색실패로_제외된다()
        {
            var provider = new FakeNameSearchProvider(new());

            var outcome = Assert.Single(await PlaceRecommendationGrounder.GroundAsync(Recommend("없는 가게"), [], provider, Now));

            Assert.Equal(GroundingStage.NameSearchNotFound, outcome.Stage);
            Assert.Equal("후보목록 불일치 → 이름검색 실패", outcome.Reason);
        }

        [Fact]
        public async Task 이름검색으로_찾았지만_폐업이면_영업여부실패로_제외된다()
        {
            var provider = new FakeNameSearchProvider(new() { ["추억의 분식집"] = [Place("추억의 분식집", closed: true)] });

            var outcome = Assert.Single(await PlaceRecommendationGrounder.GroundAsync(Recommend("추억의 분식집"), [], provider, Now));

            Assert.Equal(GroundingStage.NotOperating, outcome.Stage);
        }

        [Fact]
        public async Task 후보목록에서_폐업이어도_이름검색으로_영업중이_확인되면_채택되고_경로가_사유에_남는다()
        {
            var staleCandidate = Place("오래된 카페", lastConfirmed: Now.AddMonths(-5));
            var provider = new FakeNameSearchProvider(new() { ["오래된 카페"] = [Place("오래된 카페", lastConfirmed: Now.AddDays(-3))] });

            var outcome = Assert.Single(await PlaceRecommendationGrounder.GroundAsync(Recommend("오래된 카페"), [staleCandidate], provider, Now));

            Assert.True(outcome.Accepted);
            Assert.Equal(GroundingStage.NameSearchMatched, outcome.Stage);
            Assert.StartsWith("후보목록 일치했으나 영업여부 실패", outcome.Reason);
        }
    }
}
