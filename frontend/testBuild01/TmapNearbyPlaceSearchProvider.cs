using SharedData.DTOs;

namespace TravelApp.WebAPI.Services.PlaceSearch
{
    // Tmap(SK Open API) "주변 카테고리 검색" 연동 자리. 아직 이 프로젝트는 해당 상품 사용 신청/문서
    // 접근 권한이 없어 정확한 요청·응답 스펙을 확인하지 못했으므로, 지금은 MockNearbyPlaceSearchProvider를
    // 기본값으로 쓰고 이 클래스는 구현 전 상태로 남겨둔다.
    //
    // 실제 연동 시에는 SearchNearbyAsync 안에서 Tmap POI 주변 검색 엔드포인트를 호출해
    // PlaceSearchResultDto 목록(좌표 포함)으로 매핑하면 된다. ITravelTimeProvider/TmapTravelTimeProvider와
    // 동일한 패턴(HttpClient + Tmap:AppKey 설정)을 그대로 따르면 된다.
    // 교체는 Program.cs의 "PlaceSearch:Provider" 설정값을 "Tmap"으로 바꾸는 것만으로 끝난다.
    public class TmapNearbyPlaceSearchProvider : INearbyPlaceSearchProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _appKey;

        public TmapNearbyPlaceSearchProvider(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;

            _appKey = configuration["Tmap:AppKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_appKey))
            {
                throw new InvalidOperationException(
                    "Tmap:AppKey가 설정되지 않았습니다. SK Open API에서 주변 카테고리 검색 상품 사용 신청이 " +
                    "승인된 뒤, 다음 명령으로 키를 설정하세요:\n" +
                    "  dotnet user-secrets set \"Tmap:AppKey\" \"<YOUR_APP_KEY>\" --project backend/TravelApp.WebAPI");
            }
        }

        public Task<List<PlaceSearchResultDto>> SearchNearbyAsync(
            NearbyPlaceSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException(
                "TODO: Tmap 주변 카테고리 검색 API 연동. 승인/문서 확인 후 구현하고, " +
                "Program.cs의 PlaceSearch:Provider를 \"Tmap\"으로 바꿔 교체하세요.");
        }

        public Task<PlaceSearchResultDto?> SearchByNameAsync(
            string placeName,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException(
                "TODO: Tmap POI 이름 검색(키워드 검색) API 연동. 사용자가 언급한 특정 장소를 " +
                "현재 위치와 무관하게 검증하는 데 쓰인다.");
        }
    }
}
