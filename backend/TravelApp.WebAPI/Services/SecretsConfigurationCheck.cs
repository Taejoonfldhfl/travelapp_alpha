namespace TravelApp.WebAPI.Services
{
    // appsettings.json에는 시크릿 대신 "YOUR_..." 플레이스홀더만 둔다. 실제 값은 아래 순서로 덮어쓴다
    // (WebApplication.CreateBuilder 기본 설정 순서, 뒤가 우선):
    //   appsettings.json(플레이스홀더) < user-secrets(Development 환경에서만) < 환경변수(ConnectionStrings__DefaultConnection, Jwt__Key)
    // 앱 시작 시 플레이스홀더가 그대로 남은 시크릿을 찾아 경고한다 — 운영에 플레이스홀더 JWT 키/DB 비밀번호로 배포되는 걸 막기 위해서다.
    public static class SecretsConfigurationCheck
    {
        public const string PlaceholderMarker = "YOUR_";

        // RequiredWhen이 null이면 항상 필요, 아니면 그 조건일 때만 필요(예: 해당 provider를 켰을 때만 필요한 외부 API 키).
        private static readonly (string Key, string EnvironmentVariable, Func<IConfiguration, bool>? RequiredWhen)[] RequiredSecrets =
        [
            ("ConnectionStrings:DefaultConnection", "ConnectionStrings__DefaultConnection", null),
            ("Jwt:Key", "Jwt__Key", null),
            ("TourApi:ServiceKey", "TourApi__ServiceKey",
                c => string.Equals(c["HotelInfo:Provider"], "TourApi", StringComparison.OrdinalIgnoreCase)),
            ("Fcm:ServiceAccountJson", "Fcm__ServiceAccountJson",
                c => string.Equals(c["PushNotification:Provider"], "Fcm", StringComparison.OrdinalIgnoreCase)),
            ("Google:PlacesApiKey", "Google__PlacesApiKey",
                c => string.Equals(c["PlaceStatus:Provider"], "Google", StringComparison.OrdinalIgnoreCase)),
        ];

        // 값이 없거나 플레이스홀더가 남아 있는 시크릿의 경고 문구 목록.
        public static IReadOnlyList<string> FindPlaceholderSecrets(IConfiguration configuration) =>
            RequiredSecrets
                .Where(s => (s.RequiredWhen == null || s.RequiredWhen(configuration)) && IsPlaceholder(configuration[s.Key]))
                .Select(s => $"설정 '{s.Key}'가 비어 있거나 플레이스홀더 값 그대로입니다. " +
                             $"운영에서는 환경변수 {s.EnvironmentVariable}로, 로컬 개발에서는 " +
                             $"dotnet user-secrets set \"{s.Key}\" \"<값>\" --project backend/TravelApp.WebAPI 로 설정하세요.")
                .ToList();

        private static bool IsPlaceholder(string? value) =>
            string.IsNullOrWhiteSpace(value) || value.Contains(PlaceholderMarker, StringComparison.Ordinal);
    }
}
