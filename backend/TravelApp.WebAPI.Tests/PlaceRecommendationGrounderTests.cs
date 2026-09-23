using SharedData.DTOs;
using TravelApp.WebAPI.Services;
using Xunit;

namespace TravelApp.WebAPI.Tests
{
    // 규칙 2(좌표 없는 답변 차단)가 실제로 걸러내는지 검증한다.
    // 두 mock 시나리오를 사용한다:
    //  1) 좌표 있는 개별 장소 목록과 정확히 일치하는 추천 -> 통과, 좌표가 붙는다.
    //  2) 좌표 없이 지역/카테고리를 뭉뚱그린 추천 -> 차단, 결과에서 사라진다.
    public class PlaceRecommendationGrounderTests
    {
        private static List<PlaceSearchResultDto> BuildCandidates() =>
        [
            new PlaceSearchResultDto
            {
                PlaceId = "mock:1",
                Name = "스타벅스 리저브점",
                Category = "카페",
                Address = "중심점에서 약 220m 지점 (mock 데이터)",
                Latitude = 37.5670,
                Longitude = 126.9790
            },
            new PlaceSearchResultDto
            {
                PlaceId = "mock:2",
                Name = "계절밥상 본점",
                Category = "음식점",
                Address = "중심점에서 약 350m 지점 (mock 데이터)",
                Latitude = 37.5665,
                Longitude = 126.9800
            }
        ];

        [Fact]
        public void Ground_시나리오1_후보와_일치하는_추천은_좌표가_붙어_통과한다()
        {
            var candidates = BuildCandidates();

            var raw = new List<AiPlaceRecommendationDto>
            {
                new()
                {
                    PlaceName = "스타벅스 리저브점",
                    Description = "커피 한 잔 하기 좋은 곳",
                    SuggestedStartTime = DateTime.UtcNow,
                    SuggestedEndTime = DateTime.UtcNow.AddHours(1)
                }
            };

            var result = PlaceRecommendationGrounder.Ground(raw, candidates);

            Assert.Single(result);
            Assert.Equal("mock:1", result[0].PlaceId);
            Assert.Equal(37.5670, result[0].Latitude);
            Assert.Equal(126.9790, result[0].Longitude);
        }

        [Fact]
        public void Ground_시나리오2_좌표없이_뭉뚱그린_추천은_차단된다()
        {
            var candidates = BuildCandidates();

            var raw = new List<AiPlaceRecommendationDto>
            {
                new()
                {
                    // 후보 목록의 어떤 장소 이름과도 일치하지 않는, 지역/카테고리를 뭉뚱그린 표현.
                    PlaceName = "경복궁 주변 식당",
                    Description = "경복궁 근처에 식당이 많아요",
                    SuggestedStartTime = DateTime.UtcNow,
                    SuggestedEndTime = DateTime.UtcNow.AddHours(1)
                }
            };

            var result = PlaceRecommendationGrounder.Ground(raw, candidates);

            Assert.Empty(result);
        }

        [Fact]
        public void Ground_후보목록이_비어있으면_모든_추천이_차단된다()
        {
            var raw = new List<AiPlaceRecommendationDto>
            {
                new()
                {
                    PlaceName = "아무 카페",
                    Description = "설명",
                    SuggestedStartTime = DateTime.UtcNow,
                    SuggestedEndTime = DateTime.UtcNow.AddHours(1)
                }
            };

            var result = PlaceRecommendationGrounder.Ground(raw, new List<PlaceSearchResultDto>());

            Assert.Empty(result);
        }

        [Fact]
        public void Ground_일부만_후보와_일치하면_일치하는_것만_남는다()
        {
            var candidates = BuildCandidates();

            var raw = new List<AiPlaceRecommendationDto>
            {
                new() { PlaceName = "계절밥상 본점", Description = "d1", SuggestedStartTime = DateTime.UtcNow, SuggestedEndTime = DateTime.UtcNow.AddHours(1) },
                new() { PlaceName = "강남 일대의 맛집들", Description = "d2", SuggestedStartTime = DateTime.UtcNow, SuggestedEndTime = DateTime.UtcNow.AddHours(1) }
            };

            var result = PlaceRecommendationGrounder.Ground(raw, candidates);

            Assert.Single(result);
            Assert.Equal("계절밥상 본점", result[0].PlaceName);
            Assert.Equal("mock:2", result[0].PlaceId);
        }

        // 폐업/오래된 정보 필터(IsOperatingAsOf) 시나리오.
        [Fact]
        public void IsOperatingAsOf_폐업으로_확인된_장소는_항상_제외된다()
        {
            var place = new PlaceSearchResultDto
            {
                Name = "문닫은가게",
                IsPermanentlyClosed = true,
                LastConfirmedOperatingDate = DateTime.UtcNow // 방금 확인됐어도 폐업이면 무조건 제외.
            };

            Assert.False(PlaceRecommendationGrounder.IsOperatingAsOf(place, DateTime.UtcNow));
        }

        [Fact]
        public void IsOperatingAsOf_영업확인정보가_3개월보다_오래됐으면_제외된다()
        {
            var asOf = new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc);
            var place = new PlaceSearchResultDto
            {
                Name = "오래된정보가게",
                IsPermanentlyClosed = false,
                LastConfirmedOperatingDate = asOf.AddMonths(-4)
            };

            Assert.False(PlaceRecommendationGrounder.IsOperatingAsOf(place, asOf));
        }

        [Fact]
        public void IsOperatingAsOf_3개월_이내_영업확인정보가_있으면_통과한다()
        {
            var asOf = new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc);
            var place = new PlaceSearchResultDto
            {
                Name = "최근확인가게",
                IsPermanentlyClosed = false,
                LastConfirmedOperatingDate = asOf.AddMonths(-2)
            };

            Assert.True(PlaceRecommendationGrounder.IsOperatingAsOf(place, asOf));
        }

        [Fact]
        public void IsOperatingAsOf_영업확인정보가_아예_없으면_방금_검색된_것으로_보고_통과한다()
        {
            var place = new PlaceSearchResultDto
            {
                Name = "정보없는가게",
                IsPermanentlyClosed = false,
                LastConfirmedOperatingDate = null
            };

            Assert.True(PlaceRecommendationGrounder.IsOperatingAsOf(place, DateTime.UtcNow));
        }

        [Fact]
        public void Ground_폐업한_후보와_일치해도_차단된다()
        {
            var candidates = new List<PlaceSearchResultDto>
            {
                new()
                {
                    PlaceId = "mock:closed",
                    Name = "폐업한식당",
                    Latitude = 37.5,
                    Longitude = 127.0,
                    IsPermanentlyClosed = true
                }
            };

            var raw = new List<AiPlaceRecommendationDto>
            {
                new() { PlaceName = "폐업한식당", Description = "d", SuggestedStartTime = DateTime.UtcNow, SuggestedEndTime = DateTime.UtcNow.AddHours(1) }
            };

            var result = PlaceRecommendationGrounder.Ground(raw, candidates);

            Assert.Empty(result);
        }

        // ---- 이름 검색 결과가 여러 건일 때: 관련성 순서대로 훑어 유사도+영업여부를 모두 통과하는 첫 후보를 채택 ----

        private static readonly DateTime AskedAt = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        private static List<AiPlaceRecommendationDto> RecommendOne(string placeName) =>
            [new AiPlaceRecommendationDto { PlaceName = placeName }];

        // 이번 수정의 핵심: 실제 Tmap "몽로" 검색 결과 순서를 그대로 재현한다(1순위가 무관한 '카페로몽').
        [Fact]
        public async Task GroundAsync_몽로_1순위가_무관한_곳이어도_2순위_몽로가_채택된다()
        {
            var provider = new FakeNameSearchProvider(new()
            {
                ["몽로"] =
                [
                    FakeNameSearchProvider.Place("카페로몽"),
                    FakeNameSearchProvider.Place("몽로"),
                    FakeNameSearchProvider.Place("몽로 주차장"),
                    FakeNameSearchProvider.Place("청담몽로"),
                    FakeNameSearchProvider.Place("몽로주점"),
                ]
            });

            var outcome = Assert.Single(await PlaceRecommendationGrounder.GroundAsync(RecommendOne("몽로"), [], provider, AskedAt));

            Assert.True(outcome.Accepted);
            Assert.Equal(GroundingStage.NameSearchMatched, outcome.Stage);
            Assert.Equal(2, outcome.MatchedRank);
            Assert.Equal("id:몽로", outcome.Recommendation.PlaceId);
            Assert.Single(provider.Queries); // 추천 1건당 이름 검색은 한 번뿐(쿼터)
        }

        // 지난번 유사도 단계가 약해지지 않았는지: 실제 Tmap "광화문 우육면" 상위 5건은 전부 다른 가게라 모두 탈락해야 한다.
        [Fact]
        public async Task GroundAsync_광화문_우육면_상위5건이_전부_무관하면_유사도실패로_제외된다()
        {
            var provider = new FakeNameSearchProvider(new()
            {
                ["광화문 우육면"] =
                [
                    FakeNameSearchProvider.Place("우육면관 청계천점[중식]"),
                    FakeNameSearchProvider.Place("우육면관 광화문점"),
                    FakeNameSearchProvider.Place("덕후선생 광화문D타워점"),
                    FakeNameSearchProvider.Place("도림 더그랜드롯데서울점[중식]"),
                    FakeNameSearchProvider.Place("진중 우육면관 본점"),
                ]
            });

            var outcome = Assert.Single(await PlaceRecommendationGrounder.GroundAsync(RecommendOne("광화문 우육면"), [], provider, AskedAt));

            Assert.False(outcome.Accepted);
            Assert.Equal(GroundingStage.NameSimilarityFailed, outcome.Stage);
            Assert.Null(outcome.Recommendation.PlaceId);
            Assert.Equal(5, outcome.SearchedNames!.Count);
        }

        // d번 로직: 유사도만 통과하고 폐업인 1순위에서 채택하지 말고, 영업 중인 같은 이름의 다른 지점으로 넘어가야 한다.
        [Fact]
        public async Task GroundAsync_유사도통과했지만_폐업인_1순위는_건너뛰고_영업중인_다른지점이_채택된다()
        {
            var provider = new FakeNameSearchProvider(new()
            {
                ["몽로"] =
                [
                    FakeNameSearchProvider.Place("몽로 광화문점", closed: true),
                    FakeNameSearchProvider.Place("몽로 여의도점"),
                ]
            });

            var outcome = Assert.Single(await PlaceRecommendationGrounder.GroundAsync(RecommendOne("몽로"), [], provider, AskedAt));

            Assert.True(outcome.Accepted);
            Assert.Equal(2, outcome.MatchedRank);
            Assert.Equal("id:몽로 여의도점", outcome.Recommendation.PlaceId);
        }

        [Fact]
        public async Task GroundAsync_유사도통과한_후보가_모두_폐업이나_오래된정보면_영업여부실패로_제외된다()
        {
            var provider = new FakeNameSearchProvider(new()
            {
                ["몽로"] =
                [
                    FakeNameSearchProvider.Place("카페로몽"),
                    FakeNameSearchProvider.Place("몽로 광화문점", closed: true),
                    FakeNameSearchProvider.Place("몽로 여의도점", lastConfirmed: AskedAt.AddMonths(-6)),
                ]
            });

            var outcome = Assert.Single(await PlaceRecommendationGrounder.GroundAsync(RecommendOne("몽로"), [], provider, AskedAt));

            Assert.False(outcome.Accepted);
            Assert.Equal(GroundingStage.NotOperating, outcome.Stage);
            Assert.Equal("몽로 광화문점", outcome.FoundName);
            Assert.Null(outcome.Recommendation.PlaceId);
        }

        // ---- IsSufficientlySimilar: 행정단위 표기 정규화 ----

        [Theory]
        [InlineData("서울시청", "서울특별시청")]
        [InlineData("부산시청", "부산광역시청")]
        [InlineData("세종시청", "세종특별자치시청")]
        [InlineData("제주도청", "제주특별자치도청")]
        public void IsSufficientlySimilar_행정단위_표기만_다르면_같은_장소로_본다(string shortName, string longName)
        {
            Assert.True(PlaceRecommendationGrounder.IsSufficientlySimilar(shortName, longName));
            Assert.True(PlaceRecommendationGrounder.IsSufficientlySimilar(longName, shortName));
        }

        // 정규화가 판정 기준 자체를 느슨하게 만들지 않았는지 회귀 확인.
        [Theory]
        [InlineData("우육면관 청계천점[중식]")]
        [InlineData("우육면관 광화문점")]
        [InlineData("덕후선생 광화문D타워점")]
        [InlineData("도림 더그랜드롯데서울점[중식]")]
        [InlineData("진중 우육면관 본점")]
        public void IsSufficientlySimilar_광화문_우육면은_무관한_가게와_여전히_다르다(string tmapName)
        {
            Assert.False(PlaceRecommendationGrounder.IsSufficientlySimilar("광화문 우육면", tmapName));
        }

        // ---- TryMatch: 정확히 같은 이름의 후보를 포함 관계 매칭보다 먼저 고른다 ----

        // 실제 Tmap 주변 검색 순서: '광화문'(정문)이 '광화문광장 북측광장'보다 앞에 온다.
        private static List<PlaceSearchResultDto> GwanghwamunCandidates() =>
        [
            new PlaceSearchResultDto { PlaceId = "tmap:광화문 주차장", Name = "광화문 주차장" },
            new PlaceSearchResultDto { PlaceId = "tmap:광화문", Name = "광화문" },
            new PlaceSearchResultDto { PlaceId = "tmap:광화문광장 북측광장", Name = "광화문광장 북측광장" },
        ];

        [Theory]
        [InlineData("광화문광장 북측광장")]
        [InlineData("광화문광장북측광장")] // 공백만 다름
        public void TryMatch_같은_이름의_후보가_있으면_앞쪽의_포함관계_후보보다_먼저_고른다(string placeName)
        {
            Assert.True(PlaceRecommendationGrounder.TryMatch(placeName, GwanghwamunCandidates(), out var match));
            Assert.Equal("tmap:광화문광장 북측광장", match!.PlaceId);
        }

        [Fact]
        public void TryMatch_정확히_같은_이름이_여러_부분일치보다_뒤에_있어도_그걸_고른다()
        {
            Assert.True(PlaceRecommendationGrounder.TryMatch("광화문", GwanghwamunCandidates(), out var match));
            Assert.Equal("tmap:광화문", match!.PlaceId);
        }

        [Fact]
        public void TryMatch_짧은_후보이름을_포함한다는_이유만으로는_매칭하지_않는다()
        {
            // 예전에는 포함 관계로 '광화문 정문' -> '광화문'을 매칭했지만, 짧은 이름의 단순 포함 매칭은 '우육면당 광화문점' -> '광화문'
            // 같은 잘못된 좌표를 만들어서 허용하지 않는다(지점명만 붙은 경우와 오타 수준 차이만 인정).
            Assert.False(PlaceRecommendationGrounder.TryMatch("광화문 정문", GwanghwamunCandidates(), out _));
            Assert.False(PlaceRecommendationGrounder.TryMatch("우육면당 광화문점", GwanghwamunCandidates(), out _));
        }
    }
}
