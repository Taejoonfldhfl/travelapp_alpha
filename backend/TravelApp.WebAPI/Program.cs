using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services.Llm;
using TravelApp.WebAPI.Services.PlaceSearch;
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
            builder.Services.AddSingleton<TravelApp.WebAPI.Services.Expenses.IPushNotifier,
                TravelApp.WebAPI.Services.Expenses.LogPushNotifier>();

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
            if (string.Equals(placeSearchProviderName, "Tmap", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddScoped<INearbyPlaceSearchProvider>(
                    sp => sp.GetRequiredService<TmapNearbyPlaceSearchProvider>());
            }
            else
            {
                builder.Services.AddSingleton<INearbyPlaceSearchProvider, MockNearbyPlaceSearchProvider>();
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
