using TravelApp.WebAPI.Services.PlaceSearch;
using TravelApp.WebAPI.Services.QueryAnalysis;
using Xunit;

namespace TravelApp.WebAPI.Tests
{
    // 질문 분석: 현재 위치 검색과 지역 지정 검색을 구분하고, 카테고리를 뽑는지.
    public class TravelQueryAnalyzerTests
    {
        [Theory]
        [InlineData("근처 맛집")]
        [InlineData("근처 맛집 추천해줘")]
        [InlineData("내 주변 카페")]
        [InlineData("내 주변 카페 알려줘")]
        [InlineData("이 근처에 뭐 볼만한 거 있어?")]
        public void 지역없이_근처나_내주변만_있으면_CurrentLocation(string message)
        {
            var analysis = TravelQueryAnalyzer.Analyze(message);

            Assert.Equal(TravelQueryIntent.CurrentLocation, analysis.Intent);
            Assert.Null(analysis.Location);
            Assert.Equal(RelativeLocationKind.NearUser, analysis.RelativeLocation);
        }

        [Theory]
        [InlineData("강남 맛집", "강남", PlaceCategory.Restaurant)]
        [InlineData("강남 근처 맛집", "강남", PlaceCategory.Restaurant)]
        [InlineData("강남 근처 맛집 추천해줘", "강남", PlaceCategory.Restaurant)]
        [InlineData("강남 카페", "강남", PlaceCategory.Cafe)]
        [InlineData("강남 카페 추천", "강남", PlaceCategory.Cafe)]
        [InlineData("홍대 술집", "홍대", PlaceCategory.Bar)]
        [InlineData("경복궁 근처 한식집", "경복궁", PlaceCategory.Restaurant)]
        [InlineData("해운대 술집", "해운대", PlaceCategory.Bar)]
        [InlineData("경복궁 근처 관광지", "경복궁", PlaceCategory.Attraction)]
        [InlineData("명동 쇼핑할 곳", "명동", PlaceCategory.Shopping)]
        [InlineData("광화문에서 점심 먹을만한 곳 추천해줘", "광화문", PlaceCategory.Restaurant)]
        [InlineData("홍대 근처에서 술 한잔할 데 있을까", "홍대", PlaceCategory.Bar)]
        [InlineData("부산 자갈치시장 근처에 뭐 먹을 거 있어?", "자갈치시장", PlaceCategory.Restaurant)]
        [InlineData("성수동 카페", "성수동", PlaceCategory.Cafe)]
        public void 지역을_말하면_근처가_붙어도_NamedLocation(string message, string location, PlaceCategory category)
        {
            var analysis = TravelQueryAnalyzer.Analyze(message);

            Assert.Equal(TravelQueryIntent.NamedLocation, analysis.Intent);
            Assert.Equal(location, analysis.Location);
            Assert.Equal(category, analysis.Category);
        }

        [Fact]
        public void 경복궁_근처_한식집은_한식을_세부_키워드로_뽑는다()
        {
            var analysis = TravelQueryAnalyzer.Analyze("경복궁 근처 한식집");

            Assert.Equal("한식", analysis.CuisineKeyword);
            Assert.Contains("한식집", analysis.Keywords);
        }

        [Fact]
        public void 스타벅스_강남역점은_SpecificPlace()
        {
            var analysis = TravelQueryAnalyzer.Analyze("스타벅스 강남역점");

            Assert.Equal(TravelQueryIntent.SpecificPlace, analysis.Intent);
            Assert.Equal("스타벅스 강남역점", analysis.SpecificPlace);
        }

        [Fact]
        public void 스타벅스_강남역점_알려줘도_SpecificPlace()
        {
            Assert.Equal(TravelQueryIntent.SpecificPlace, TravelQueryAnalyzer.Analyze("스타벅스 강남역점 알려줘").Intent);
        }

        [Fact]
        public void 특정_가게_가는_길의_카페는_그_가게를_기준으로_카페를_찾는다()
        {
            var analysis = TravelQueryAnalyzer.Analyze("우육면당 광화문점 가는 길에 들를만한 카페 있어?");

            Assert.Equal(TravelQueryIntent.SpecificPlace, analysis.Intent);
            Assert.Equal("우육면당 광화문점", analysis.SpecificPlace);
            Assert.Equal(PlaceCategory.Cafe, analysis.Category);
        }

        [Fact]
        public void X에_있는_Y는_Y를_특정장소로_X를_지역단서로_뽑는다()
        {
            var analysis = TravelQueryAnalyzer.Analyze("광화문에 있는 몽로 어때? 거기 기준으로 다른 곳도 추천해줘");

            Assert.Equal(TravelQueryIntent.SpecificPlace, analysis.Intent);
            Assert.Equal("몽로", analysis.SpecificPlace);
            Assert.Equal("광화문", analysis.Location);
        }

        [Theory]
        [InlineData("이번 여행 맛집 추천", PlaceCategory.Restaurant)]
        [InlineData("이번 여행에서 맛집 추천해줘", PlaceCategory.Restaurant)]
        [InlineData("추천해줘", PlaceCategory.Other)]
        public void 위치_언급없는_추천은_GeneralRecommendation(string message, PlaceCategory category)
        {
            var analysis = TravelQueryAnalyzer.Analyze(message);

            Assert.Equal(TravelQueryIntent.GeneralRecommendation, analysis.Intent);
            Assert.Null(analysis.Location);
            Assert.Equal(category, analysis.Category);
        }

        [Fact]
        public void 오늘_일정_근처_맛집은_일정_위치_기준_일반추천()
        {
            var analysis = TravelQueryAnalyzer.Analyze("오늘 일정 근처 맛집");

            Assert.Equal(TravelQueryIntent.GeneralRecommendation, analysis.Intent);
            Assert.Equal(RelativeLocationKind.NearSchedule, analysis.RelativeLocation);
            Assert.True(analysis.MentionsToday);
            Assert.Equal(PlaceCategory.Restaurant, analysis.Category);
        }

        [Theory]
        [InlineData("오늘 일정 알려줘")]
        [InlineData("내일 날씨 어때?")]
        public void 장소를_찾지_않는_질문은_NonRecommendation(string message)
        {
            Assert.Equal(TravelQueryIntent.NonRecommendation, TravelQueryAnalyzer.Analyze(message).Intent);
        }

        [Theory]
        [InlineData("강남에서 파스타 먹고 싶어", "파스타", PlaceCategory.Restaurant)]
        [InlineData("홍대 근처 디저트", "디저트", PlaceCategory.Cafe)]
        [InlineData("훠궈 먹고 싶어", "훠궈", PlaceCategory.Restaurant)] // 사전에 없는 메뉴도 "X 먹고"의 X로 뽑는다
        public void 업종보다_좁은_메뉴를_뽑는다(string message, string menu, PlaceCategory category)
        {
            var analysis = TravelQueryAnalyzer.Analyze(message);

            Assert.Equal(menu, analysis.MenuKeyword);
            Assert.Equal(category, analysis.Category);
        }

        [Theory]
        [InlineData("광화문에서 점심 먹을만한 곳 추천해줘")]
        [InlineData("부산 자갈치시장 근처에 뭐 먹을 거 있어?")]
        [InlineData("경복궁 근처 한식집")]
        [InlineData("우육면당 광화문점 가는 길에 들를만한 카페 있어?")] // 가게 이름 속 '우육면'은 메뉴가 아니다
        public void 일반적인_음식_표현이나_가게이름은_메뉴로_보지_않는다(string message)
        {
            Assert.Null(TravelQueryAnalyzer.Analyze(message).MenuKeyword);
        }
    }
}
