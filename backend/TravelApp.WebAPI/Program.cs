using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services.Expenses;
using TravelApp.WebAPI.Services.HotelInfo;
using TravelApp.WebAPI.Services.Llm;
using TravelApp.WebAPI.Services.PlaceImage;
using TravelApp.WebAPI.Services.PlaceSearch;
using TravelApp.WebAPI.Services.PlaceStatus;
using TravelApp.WebAPI.Services.Push;
using TravelApp.WebAPI.Services.RouteOptimization;

namespace TravelApp.WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // 설정 우선순위(뒤가 우선): appsettings.json(플레이스홀더) < user-secrets(Development 환경) < 환경변수.
            // CreateBuilder의 기본 동작이 이 순서라 별도 등록은 하지 않는다. 시크릿 검사는 아래 SecretsConfigurationCheck 참고.
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers()
                .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter()));

            // 서버발 푸시: 설정("PushNotification:Provider")으로 Log(기본, 콘솔 로그만)/Fcm 전환.
            // Fcm을 선택했는데 서비스 계정 키(Fcm:ServiceAccountJson)가 없거나 초기화에 실패하면
            // 앱을 죽이지 않고 Log로 폴백한다. 경고는 app.Logger가 준비된 뒤 남긴다(아래 참고).
            string? fcmFallbackReason = null;
            var pushProviderName = builder.Configuration["PushNotification:Provider"] ?? "Log";
            if (string.Equals(pushProviderName, "Fcm", StringComparison.OrdinalIgnoreCase))
            {
                string serviceAccountJson = builder.Configuration["Fcm:ServiceAccountJson"] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(serviceAccountJson) ||
                    serviceAccountJson.Contains(Services.SecretsConfigurationCheck.PlaceholderMarker, StringComparison.Ordinal))
                {
                    fcmFallbackReason = "Fcm:ServiceAccountJson이 설정되지 않았습니다";
                }
                else
                {
                    try
                    {
                        if (FirebaseApp.DefaultInstance == null)
                        {
                            FirebaseApp.Create(new AppOptions
                            {
                                Credential = CredentialFactory.FromJson<ServiceAccountCredential>(serviceAccountJson).ToGoogleCredential()
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        fcmFallbackReason = $"Firebase 초기화에 실패했습니다({ex.Message})";
                    }
                }
            }
            else if (!string.Equals(pushProviderName, "Log", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"알 수 없는 PushNotification:Provider '{pushProviderName}'입니다. 현재 지원: Log, Fcm");
            }

            if (string.Equals(pushProviderName, "Fcm", StringComparison.OrdinalIgnoreCase) && fcmFallbackReason == null)
            {
                builder.Services.AddSingleton<IFcmSender, FirebaseFcmSender>();
                builder.Services.AddScoped<IPushNotifier, FcmPushNotifier>();
            }
            else
            {
                builder.Services.AddSingleton<IPushNotifier, LogPushNotifier>();
            }

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            // builder.Services.AddOpenApi();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] {}
                    }
                });
            });
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
            builder.Services.AddHttpClient<AnthropicLlmClient>();
            builder.Services.AddHostedService<AnthropicStartupValidator>();

            // 경로 최적화: 이동시간 provider는 설정("RouteOptimization:Provider")으로 교체 가능하게 분리.
            // Tmap 사용 권한이 승인되기 전까지는 Haversine(직선거리 추정)이 기본값이다.
            builder.Services.AddMemoryCache();
            builder.Services.AddHttpClient<TmapTravelTimeProvider>();

            var travelTimeProviderName = builder.Configuration["RouteOptimization:Provider"] ?? "Haversine";
            if (string.Equals(travelTimeProviderName, "Tmap", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddScoped<ITravelTimeProvider>(
                    sp => sp.GetRequiredService<TmapTravelTimeProvider>());
            }
            else
            {
                builder.Services.AddSingleton<ITravelTimeProvider, HaversineTravelTimeProvider>();
            }

            builder.Services.AddScoped<RouteOptimizationService>();

            // 장소 추천(AI 챗봇)의 주변 카테고리 검색: 설정("PlaceSearch:Provider")으로 교체 가능하게 분리.
            // Tmap 사용 권한이 승인되기 전까지는 Mock이 기본값이다.
            builder.Services.AddHttpClient<TmapNearbyPlaceSearchProvider>();

            var placeSearchProviderName = builder.Configuration["PlaceSearch:Provider"] ?? "Mock";
            var mockNearbyPlaceSearchProvider = new MockNearbyPlaceSearchProvider();
            Func<IServiceProvider, INearbyPlaceSearchProvider> resolveInnerPlaceSearchProvider =
                string.Equals(placeSearchProviderName, "Tmap", StringComparison.OrdinalIgnoreCase)
                    ? sp => sp.GetRequiredService<TmapNearbyPlaceSearchProvider>()
                    : _ => mockNearbyPlaceSearchProvider;

            // 장소 영업상태(휴관/폐업) 보완: 설정("PlaceStatus:Provider")으로 Mock(항상 확인 안 함, 기본값)/Google 전환.
            // Tmap POI 응답에는 영업상태 필드가 없어 위 검색 결과는 항상 Unknown인데, Google이면 위에서 고른
            // 안쪽 provider(resolveInnerPlaceSearchProvider) 결과 중 Unknown인 것만 Google Places로 보완하는
            // StatusEnrichingNearbyPlaceSearchProvider로 감싼다. Mock(기본값)이면 기존 INearbyPlaceSearchProvider
            // 등록을 그대로 두어 동작 변경이 없다.
            var placeStatusProviderName = builder.Configuration["PlaceStatus:Provider"] ?? "Mock";
            if (string.Equals(placeStatusProviderName, "Google", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddHttpClient<GooglePlacesOperatingStatusProvider>();
                builder.Services.AddScoped<IPlaceOperatingStatusProvider>(
                    sp => sp.GetRequiredService<GooglePlacesOperatingStatusProvider>());

                builder.Services.AddScoped<INearbyPlaceSearchProvider>(sp =>
                    new StatusEnrichingNearbyPlaceSearchProvider(
                        resolveInnerPlaceSearchProvider(sp),
                        sp.GetRequiredService<IPlaceOperatingStatusProvider>(),
                        sp.GetRequiredService<ILogger<StatusEnrichingNearbyPlaceSearchProvider>>()));
            }
            else if (string.Equals(placeStatusProviderName, "Mock", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddSingleton<IPlaceOperatingStatusProvider, MockPlaceOperatingStatusProvider>();
                builder.Services.AddScoped<INearbyPlaceSearchProvider>(resolveInnerPlaceSearchProvider);
            }
            else
            {
                throw new InvalidOperationException(
                    $"알 수 없는 PlaceStatus:Provider '{placeStatusProviderName}'입니다. 현재 지원: Mock, Google");
            }

            // 챗봇 추천 카드의 장소 대표 사진: 설정("PlaceImage:Provider")으로 교체 가능하게 분리.
            // 사진 API 키가 준비되기 전까지는 Mock(항상 사진 없음)뿐이다. 실제 provider를 추가하면 여기에 분기를 더한다.
            var placeImageProviderName = builder.Configuration["PlaceImage:Provider"] ?? "Mock";
            if (!string.Equals(placeImageProviderName, "Mock", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"알 수 없는 PlaceImage:Provider '{placeImageProviderName}'입니다. 현재 지원: Mock");
            }
            builder.Services.AddSingleton<IPlaceImageProvider, MockPlaceImageProvider>();

            // 숙박시설 정보(TourAPI 4.0): 설정("HotelInfo:Provider")으로 교체. TourAPI 인증키가 없는 동안은 Mock이 기본값이다.
            // 인증키(TourApi:ServiceKey)는 다른 시크릿과 같은 순서(환경변수 > user-secrets > appsettings.json)로 읽는다.
            var hotelInfoProviderName = builder.Configuration["HotelInfo:Provider"] ?? "Mock";
            if (string.Equals(hotelInfoProviderName, "TourApi", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddHttpClient<TourApiHotelInfoProvider>(client => client.Timeout = TimeSpan.FromSeconds(10));
                builder.Services.AddScoped<IHotelInfoProvider>(sp => sp.GetRequiredService<TourApiHotelInfoProvider>());
            }
            else if (string.Equals(hotelInfoProviderName, "Mock", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddSingleton<IHotelInfoProvider, MockHotelInfoProvider>();
            }
            else
            {
                throw new InvalidOperationException(
                    $"알 수 없는 HotelInfo:Provider '{hotelInfoProviderName}'입니다. 현재 지원: Mock, TourApi");
            }

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,      // 발행자 검증
                        ValidIssuer = builder.Configuration.GetSection("Jwt:Issuer").Value,
                        ValidateAudience = true,    // 대상자 검증
                        ValidAudience = builder.Configuration.GetSection("Jwt:Audience").Value,
                        ValidateLifetime = true,    // 토큰 유효기간 검증
                        ValidateIssuerSigningKey = true,    // 비밀키 검증
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration.GetSection("Jwt:Key").Value!))
                    };
                });
            var app = builder.Build();

            // Fcm을 선택했는데 서비스 계정 키가 없거나 초기화에 실패해 Log로 폴백한 경우, 여기서 경고를 남긴다.
            if (fcmFallbackReason != null)
            {
                app.Logger.LogWarning(
                    "PushNotification:Provider가 Fcm으로 설정되었지만 {Reason}. Log(Mock)로 대체합니다.",
                    fcmFallbackReason);
            }

            // 플레이스홀더 시크릿이 그대로 남아 있으면 시작 시점에 경고한다(운영 배포 실수 방지).
            foreach (var warning in Services.SecretsConfigurationCheck.FindPlaceholderSecrets(app.Configuration))
            {
                app.Logger.LogWarning("{SecretWarning}", warning);
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                // app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
