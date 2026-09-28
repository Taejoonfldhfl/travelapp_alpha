using Microsoft.Extensions.Configuration;
using TravelApp.WebAPI.Services;

namespace TravelApp.WebAPI.Tests;

// 시크릿 분리: appsettings.json의 플레이스홀더가 그대로 남았으면 경고하고, 환경변수/user-secrets 값이 있으면 경고하지 않는다.
public class SecretsConfigurationCheckTests
{
    private const string PlaceholderConnection = "Host=localhost;Port=5432;Database=TravelDb;Username=postgres;Password=YOUR_DB_PASSWORD";
    private const string PlaceholderJwtKey = "YOUR_SUPER_SECRET_KEY_AT_LEAST_32_CHARS_LONG";

    private static IConfiguration Config(string? connection, string? jwtKey, Dictionary<string, string?>? overrides = null)
    {
        var builder = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection,
            ["Jwt:Key"] = jwtKey
        });

        if (overrides != null)
        {
            builder.AddInMemoryCollection(overrides); // 나중에 추가한 소스가 우선(user-secrets/환경변수 역할)
        }

        return builder.Build();
    }

    [Fact]
    public void 플레이스홀더가_그대로면_두_시크릿_모두_경고한다()
    {
        var warnings = SecretsConfigurationCheck.FindPlaceholderSecrets(Config(PlaceholderConnection, PlaceholderJwtKey));

        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("ConnectionStrings:DefaultConnection") && w.Contains("ConnectionStrings__DefaultConnection"));
        Assert.Contains(warnings, w => w.Contains("Jwt:Key") && w.Contains("Jwt__Key"));
    }

    [Fact]
    public void 값이_아예_없어도_경고한다()
    {
        Assert.Equal(2, SecretsConfigurationCheck.FindPlaceholderSecrets(Config(null, "  ")).Count);
    }

    [Fact]
    public void 뒤에_추가된_설정소스가_실제값으로_덮어쓰면_경고하지_않는다()
    {
        var configuration = Config(PlaceholderConnection, PlaceholderJwtKey, new()
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=db;Database=TravelDb;Username=app;Password=real-password",
            ["Jwt:Key"] = "a-real-signing-key-with-more-than-32-characters"
        });

        Assert.Empty(SecretsConfigurationCheck.FindPlaceholderSecrets(configuration));
    }

    [Fact]
    public void 하나만_덮어쓰면_나머지_하나만_경고한다()
    {
        var configuration = Config(PlaceholderConnection, PlaceholderJwtKey, new()
        {
            ["Jwt:Key"] = "a-real-signing-key-with-more-than-32-characters"
        });

        var warning = Assert.Single(SecretsConfigurationCheck.FindPlaceholderSecrets(configuration));
        Assert.Contains("ConnectionStrings:DefaultConnection", warning);
    }

    [Fact]
    public void 저장소의_appsettings_json에는_실제_시크릿이_아니라_플레이스홀더만_있다()
    {
        string path = Path.GetFullPath(Path.Combine(SourceDirectory(), "..", "TravelApp.WebAPI", "appsettings.json"));
        var configuration = new ConfigurationBuilder().AddJsonFile(path).Build();

        Assert.Contains(SecretsConfigurationCheck.PlaceholderMarker, configuration["ConnectionStrings:DefaultConnection"]);
        Assert.Contains(SecretsConfigurationCheck.PlaceholderMarker, configuration["Jwt:Key"]);
        Assert.Null(configuration["Tmap:AppKey"]);
        Assert.Null(configuration["Anthropic:ApiKey"]);
        Assert.True(string.IsNullOrEmpty(configuration["TourApi:ServiceKey"]));
        Assert.Equal("Mock", configuration["HotelInfo:Provider"]);
    }

    [Theory]
    [InlineData("Mock", "", 0)]           // Mock이면 TourAPI 키가 없어도 경고하지 않는다
    [InlineData(null, "", 0)]             // Provider 미설정 = Mock
    [InlineData("TourApi", "", 1)]        // TourAPI를 켰는데 키가 없으면 경고
    [InlineData("tourapi", "YOUR_KEY", 1)]
    [InlineData("TourApi", "real-service-key", 0)]
    public void TourAPI_키는_HotelInfo_Provider가_TourApi일_때만_검사한다(string? provider, string serviceKey, int expectedTourApiWarnings)
    {
        var configuration = Config("Host=db;Password=real", "a-real-signing-key-with-more-than-32-characters", new()
        {
            ["HotelInfo:Provider"] = provider,
            ["TourApi:ServiceKey"] = serviceKey
        });

        var warnings = SecretsConfigurationCheck.FindPlaceholderSecrets(configuration);

        Assert.Equal(expectedTourApiWarnings, warnings.Count(w => w.Contains("TourApi:ServiceKey") && w.Contains("TourApi__ServiceKey")));
        Assert.Equal(expectedTourApiWarnings, warnings.Count);
    }

    // 빌드 결과 위치(--artifacts-path 등)와 무관하게 이 소스 파일 기준으로 appsettings.json을 찾는다.
    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.GetDirectoryName(path)!;
}
