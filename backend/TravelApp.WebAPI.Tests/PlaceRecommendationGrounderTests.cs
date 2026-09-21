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
    }
}
