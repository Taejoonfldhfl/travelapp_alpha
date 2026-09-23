using SharedData.DTOs;
using SharedData.Models;
using TravelApp.WebAPI.Services;
using TravelApp.WebAPI.Services.PlaceSearch;
using TravelApp.WebAPI.Services.QueryAnalysis;
using Xunit;
using static TravelApp.WebAPI.Tests.FakeNameSearchProvider;

namespace TravelApp.WebAPI.Tests
{
    // 검색 기준점 결정과 후보 정리: "근처"는 GPS, "강남 근처"는 강남. 실패 시 의도별 이유를 돌려준다.
    public class PlaceCandidateSearchServiceTests
    {
        private static readonly DateTime Now = new(2026, 9, 23, 3, 0, 0, DateTimeKind.Utc);

        // 사용자가 광화문 부근에 있다고 가정한 GPS. 강남역과는 약 9km 떨어져 있다.
        private const double GpsLatitude = 37.5759;
        private const double GpsLongitude = 126.9769;
        private const double GangnamStationLatitude = 37.4979;
        private const double GangnamStationLongitude = 127.0276;

        private static FakeNameSearchProvider ProviderWithGangnam() => new(new()
        {
            ["강남역"] =
            [
                Place("강남역", latitude: GangnamStationLatitude, longitude: GangnamStationLongitude, category: "교통편의 > 지하철"),
                Place("강남역 1번출구", latitude: 37.4981, longitude: 127.0279),
            ]
        });

        private static PlaceCandidateSearchService Service(FakeNameSearchProvider provider) =>
            new(provider, new LocationResolver(provider));

        private static Task<CandidateSearchResult> Search(
            FakeNameSearchProvider provider, string message, bool withGps = true, IReadOnlyList<Schedule>? schedules = null) =>
            Service(provider).SearchAsync(TravelQueryAnalyzer.Analyze(message),
                withGps ? GpsLatitude : null, withGps ? GpsLongitude : null, schedules ?? [], Now);

        [Fact]
        public async Task 강남_근처_맛집은_GPS가_있어도_강남역을_중심으로_음식점을_검색한다()
        {
            var provider = ProviderWithGangnam();
            provider.NearbyResults = [Place("강남 맛집 하나", latitude: 37.4985, longitude: 127.0280)];

            var result = await Search(provider, "강남 근처 맛집");

            // 후보가 1개뿐이라 반경을 넓혀 한 번 더 검색하지만, 두 번 모두 강남역 중심·음식점 조건이다.
            Assert.NotEmpty(provider.NearbyRequests);
            Assert.All(provider.NearbyRequests, request =>
            {
                Assert.Equal(GangnamStationLatitude, request.Latitude);
                Assert.Equal(GangnamStationLongitude, request.Longitude);
                Assert.Equal(PlaceCategory.Restaurant, request.Category);
            });
            Assert.Equal(SearchAnchorSource.NamedLocation, result.Anchor!.Source);
            Assert.Equal("강남역", result.Anchor.Label);
            Assert.Equal(ChatSearchStatus.Success, result.Status);
        }

        [Fact]
        public async Task 근처_맛집은_GPS를_중심으로_검색한다()
        {
            var provider = ProviderWithGangnam();
            provider.NearbyResults = [Place("광화문 식당", latitude: 37.5760, longitude: 126.9770)];

            var result = await Search(provider, "근처 맛집");

            Assert.All(provider.NearbyRequests, request =>
            {
                Assert.Equal(GpsLatitude, request.Latitude);
                Assert.Equal(GpsLongitude, request.Longitude);
            });
            Assert.Equal(SearchAnchorSource.CurrentLocation, result.Anchor!.Source);
            Assert.Empty(provider.Queries); // 지역 이름 검색을 하지 않는다
        }

        [Fact]
        public async Task 근처를_물었는데_GPS가_없으면_위치를_지어내지_않고_이유를_알린다()
        {
            var provider = ProviderWithGangnam();

            var result = await Search(provider, "근처 맛집", withGps: false);

            Assert.Equal(ChatSearchStatus.LocationUnavailable, result.Status);
            Assert.Empty(provider.NearbyRequests);
            Assert.Contains("현재 위치를 확인할 수 없어요", result.UserMessage);
        }

        [Fact]
        public async Task 검색결과가_0개면_의도에_맞는_실패이유를_주고_추가질문을_하지_않는다()
        {
            var provider = ProviderWithGangnam();

            var named = await Search(provider, "강남 맛집");
            var current = await Search(provider, "근처 카페");

            Assert.Equal(ChatSearchStatus.NoCandidates, named.Status);
            Assert.Equal("강남 지역에서 조건에 맞는 음식점을 찾지 못했어요.", named.UserMessage);
            Assert.Equal(ChatSearchStatus.NoCandidates, current.Status);
            Assert.Equal("현재 위치 주변에서 조건에 맞는 카페를 찾지 못했어요.", current.UserMessage);
            Assert.DoesNotContain("구체적", named.UserMessage);
            Assert.DoesNotContain("구체적", current.UserMessage);
        }

        [Fact]
        public async Task 후보가_모자라면_반경을_한번만_넓혀_다시_검색한다()
        {
            var provider = ProviderWithGangnam();
            provider.NearbyResults = [Place("하나뿐인 식당", latitude: 37.4985, longitude: 127.0280)];

            var result = await Search(provider, "강남 맛집");

            Assert.Equal(2, provider.NearbyRequests.Count);
            Assert.True(provider.NearbyRequests[1].RadiusMeters > provider.NearbyRequests[0].RadiusMeters);
            Assert.Single(result.Candidates); // 두 번 받아도 중복은 하나로
        }

        [Fact]
        public async Task 지역_좌표를_확정하지_못하면_검색하지_않고_이유를_알린다()
        {
            var provider = new FakeNameSearchProvider(new());

            var result = await Search(provider, "강남 맛집");

            Assert.Equal(ChatSearchStatus.LocationNotResolved, result.Status);
            Assert.Empty(provider.NearbyRequests);
        }

        [Fact]
        public async Task 존재하지_않는_특정장소는_다른_지점으로_바꿔치기하지_않고_실패한다()
        {
            // 실제 Tmap "스타벅스 강남역점" 검색 결과: 정확히 그 지점은 없고 다른 강남 지점들만 나온다.
            var provider = new FakeNameSearchProvider(new()
            {
                ["스타벅스 강남역점"] =
                [
                    Place("스타벅스 강남역우송빌딩점"),
                    Place("스타벅스 강남역신분당역사점"),
                    Place("스타벅스 강남에비뉴점"),
                ]
            });

            var result = await Search(provider, "스타벅스 강남역점 알려줘");

            Assert.Equal(ChatSearchStatus.LocationNotResolved, result.Status);
            Assert.Equal("말씀하신 장소('스타벅스 강남역점')를 정확하게 확인하지 못했어요.", result.UserMessage);
            Assert.Empty(provider.NearbyRequests);
        }

        [Fact]
        public async Task 특정장소를_찾으면_그_장소가_첫_후보가_된다()
        {
            var provider = new FakeNameSearchProvider(new() { ["스타벅스 강남역점"] = [Place("스타벅스 강남역점")] });
            provider.NearbyResults = [Place("다른 카페", latitude: 37.571, longitude: 126.981)];

            var result = await Search(provider, "스타벅스 강남역점 알려줘");

            Assert.Equal(ChatSearchStatus.Success, result.Status);
            Assert.Equal("스타벅스 강남역점", result.Candidates[0].Name);
            Assert.Equal(SearchAnchorSource.SpecificPlace, result.Anchor!.Source);
        }

        [Fact]
        public async Task 지역_단서와_다른_지역의_동명_가게는_인정하지_않는다()
        {
            // "광화문에 있는 몽로": 이름 검색에 여의도 몽로만 있으면 광화문(약 7km 밖) 몽로로 인정하지 않는다.
            var provider = new FakeNameSearchProvider(new()
            {
                ["광화문"] = [Place("광화문", latitude: 37.5760, longitude: 126.9769, category: "여행/레저 > 관광명소 > 문화유적지")],
                ["몽로"] = [Place("카페로몽", latitude: 37.55, longitude: 126.92), Place("몽로", latitude: 37.5219, longitude: 126.9245)]
            });

            var result = await Search(provider, "광화문에 있는 몽로 어때?");

            Assert.Equal(ChatSearchStatus.LocationNotResolved, result.Status);
        }

        [Fact]
        public async Task 검색_API가_실패하면_검색결과_없음과_구분해_알린다()
        {
            var provider = ProviderWithGangnam();
            provider.ThrowOnSearch = new InvalidOperationException("Tmap POI 검색 API 호출이 실패했습니다 (500)");

            var result = await Search(provider, "근처 맛집");

            Assert.Equal(ChatSearchStatus.SearchUnavailable, result.Status);
            Assert.Equal("장소 검색 서비스를 일시적으로 사용할 수 없어요. 잠시 후 다시 시도해 주세요.", result.UserMessage);
            Assert.Contains("500", result.ErrorDetail);
        }

        [Fact]
        public async Task 부속시설과_폐업장소는_후보에서_빠지고_같은_브랜드_다른_지점은_남는다()
        {
            var provider = ProviderWithGangnam();
            provider.NearbyResults =
            [
                Place("스타벅스 강남역점", latitude: 37.4981, longitude: 127.0277),
                Place("스타벅스 강남역점", latitude: 37.4981, longitude: 127.0277), // 완전히 같은 결과(중복)
                Place("스타벅스 역삼점", latitude: 37.5000, longitude: 127.0360),
                Place("스타벅스 강남역점 주차장", latitude: 37.4982, longitude: 127.0278),
                Place("문 닫은 식당", closed: true, latitude: 37.4983, longitude: 127.0279),
            ];

            var result = await Search(provider, "강남 카페");

            Assert.Equal(["스타벅스 강남역점", "스타벅스 역삼점"], result.Candidates.Select(c => c.Name).ToArray());
            Assert.All(result.Candidates, c => Assert.NotNull(c.DistanceMeters));
        }

        [Fact]
        public async Task 여행_일반추천은_GPS보다_일정_지역을_기준으로_한다()
        {
            var provider = ProviderWithGangnam();
            provider.NearbyResults = [Place("부산 식당", latitude: 35.16, longitude: 129.16)];
            var schedules = new List<Schedule>
            {
                new() { Title = "해운대", Latitude = 35.1587, Longitude = 129.1604, StartTime = Now, EndTime = Now.AddHours(1) }
            };

            var result = await Search(provider, "이번 여행 맛집 추천", schedules: schedules);

            Assert.Equal(SearchAnchorSource.TripArea, result.Anchor!.Source);
            Assert.Equal(35.1587, provider.NearbyRequests[0].Latitude);
        }

        [Fact]
        public async Task 오늘_일정_근처는_오늘_일정_위치를_기준으로_한다()
        {
            var provider = ProviderWithGangnam();
            provider.NearbyResults = [Place("식당", latitude: 37.58, longitude: 126.98)];
            var schedules = new List<Schedule>
            {
                new() { Title = "어제", Latitude = 35.0, Longitude = 129.0, StartTime = Now.AddDays(-1), EndTime = Now.AddDays(-1).AddHours(1) },
                new() { Title = "오늘", Latitude = 37.5796, Longitude = 126.9770, StartTime = Now, EndTime = Now.AddHours(1) }
            };

            var result = await Search(provider, "오늘 일정 근처 맛집", schedules: schedules);

            Assert.Equal(SearchAnchorSource.ScheduleArea, result.Anchor!.Source);
            Assert.Equal(37.5796, provider.NearbyRequests[0].Latitude);
        }

        [Fact]
        public async Task 파스타는_업종필터가_아니라_키워드검색으로_보낸다()
        {
            var provider = ProviderWithGangnam();
            provider.NearbyResults = [Place("파스타우연", latitude: 37.4985, longitude: 127.0280)];

            await Search(provider, "강남에서 파스타 먹고 싶어");

            Assert.All(provider.NearbyRequests, request =>
            {
                Assert.Equal("파스타", request.SearchText);
                Assert.Null(request.Keyword);
            });
        }

        [Fact]
        public async Task 한식처럼_업종명인_키워드는_기존_업종검색으로_보낸다()
        {
            var provider = ProviderWithGangnam();
            provider.NearbyResults = [Place("한식당", latitude: 37.4985, longitude: 127.0280)];

            await Search(provider, "강남 한식집");

            Assert.All(provider.NearbyRequests, request =>
            {
                Assert.Equal("한식", request.Keyword);
                Assert.Null(request.SearchText);
            });
        }

        [Fact]
        public async Task Mock_provider에서_파스타_요청은_파스타_가게로만_좁혀진다()
        {
            var provider = new MockNearbyPlaceSearchProvider();
            var service = new PlaceCandidateSearchService(provider, new LocationResolver(provider));

            var pasta = await service.SearchAsync(TravelQueryAnalyzer.Analyze("근처 파스타 먹고 싶어"), GpsLatitude, GpsLongitude, [], Now);
            var anyFood = await service.SearchAsync(TravelQueryAnalyzer.Analyze("근처 맛집"), GpsLatitude, GpsLongitude, [], Now);

            Assert.Equal(["파스타 하우스", "트라토리아 로마"], pasta.Candidates.Select(c => c.Name).ToArray());
            Assert.DoesNotContain(anyFood.Candidates, c => c.Name.Contains("파스타"));
            Assert.Contains(anyFood.Candidates, c => c.Name == "계절밥상 본점");
        }

        [Fact]
        public async Task 메뉴로_찾았는데_0건이면_메뉴를_밝혀_알린다()
        {
            var provider = ProviderWithGangnam();

            var result = await Search(provider, "강남에서 파스타 먹고 싶어");

            Assert.Equal(ChatSearchStatus.NoCandidates, result.Status);
            Assert.Equal("강남 지역에서 조건에 맞는 '파스타' 관련 장소를 찾지 못했어요.", result.UserMessage);
        }

        [Fact]
        public async Task 이미_일정에_있는_장소는_후보에서_빠진다()
        {
            var provider = ProviderWithGangnam();
            provider.NearbyResults =
            [
                Place("경복궁", latitude: 37.5796, longitude: 126.9770),
                Place("흥례문", latitude: 37.5780, longitude: 126.9770),
                Place("광화문광장 북측광장", latitude: 37.5740, longitude: 126.9769),
            ];
            var schedules = new List<Schedule>
            {
                new() { Title = "경복궁 관람", PlaceName = "경복궁", StartTime = Now, EndTime = Now.AddHours(2) },
                new() { Title = "광화문광장 산책", PlaceName = "광화문광장", StartTime = Now.AddHours(3), EndTime = Now.AddHours(4) },
            };

            var result = await Search(provider, "이 근처에 뭐 볼만한 거 있어?", schedules: schedules);

            // '경복궁'은 일정과 이름이 같아 빠지고, '광화문광장 북측광장'은 일정('광화문광장')과 이름이 달라 남는다.
            Assert.Equal(["광화문광장 북측광장", "흥례문"], result.Candidates.Select(c => c.Name).ToArray()); // 거리순
            Assert.Equal(1, result.ExcludedAsScheduled);
        }

        [Fact]
        public async Task 사용자가_직접_물은_장소는_일정에_있어도_기준장소로_남는다()
        {
            var provider = new FakeNameSearchProvider(new() { ["스타벅스 강남역점"] = [Place("스타벅스 강남역점")] });
            var schedules = new List<Schedule> { new() { Title = "카페", PlaceName = "스타벅스 강남역점", StartTime = Now, EndTime = Now.AddHours(1) } };

            var result = await Search(provider, "스타벅스 강남역점 알려줘", schedules: schedules);

            Assert.Equal("스타벅스 강남역점", result.Candidates[0].Name);
        }
    }
}
