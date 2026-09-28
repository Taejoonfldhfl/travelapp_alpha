using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SharedData.DTOs;
using SharedData.Models;
using TravelApp.WebAPI.Controllers;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services;

namespace TravelApp.WebAPI.Tests;

// 이메일은 대소문자/앞뒤 공백과 무관하게 같은 계정이다: 가입 시 정규화해 저장하고, 로그인/중복 확인/초대는 양쪽을 정규화해 비교한다.
public class EmailNormalizationTests
{
    private const string Password = "password123";

    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static UserController UserController(ApplicationDbContext db)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-signing-key-that-is-at-least-32-bytes-long",
                ["Jwt:Issuer"] = "test",
                ["Jwt:Audience"] = "test"
            })
            .Build();
        return new UserController(db, configuration);
    }

    private static Task<ActionResult<UserResponseDto>> Register(UserController controller, string email) =>
        controller.Register(new UserRegisterDto { Email = email, Password = Password, Nickname = "tester" });

    private static Task<ActionResult<UserResponseDto>> Login(UserController controller, string email, string password = Password) =>
        controller.Login(new UserLoginDto { Email = email, Password = password });

    [Theory]
    [InlineData("User@Example.COM", "user@example.com")]
    [InlineData("  user@example.com  ", "user@example.com")]
    [InlineData("\tMiXeD@Example.com\n", "mixed@example.com")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeEmail_앞뒤_공백을_지우고_소문자로_바꾼다(string? input, string expected)
    {
        Assert.Equal(expected, input.NormalizeEmail());
    }

    [Fact]
    public async Task 가입하면_정규화된_소문자_이메일로_저장된다()
    {
        using var db = CreateDb();

        await Register(UserController(db), "  Traveler@Example.COM ");

        Assert.Equal("traveler@example.com", Assert.Single(db.Users).Email);
    }

    [Theory]
    [InlineData("traveler@example.com")]
    [InlineData("TRAVELER@EXAMPLE.COM")]
    [InlineData("tRaVeLeR@eXaMpLe.CoM")]
    [InlineData("  Traveler@Example.com  ")]
    public async Task 대소문자_섞어_가입한_계정에_다른_대소문자_조합과_공백으로_로그인할_수_있다(string loginEmail)
    {
        using var db = CreateDb();
        var controller = UserController(db);
        await Register(controller, "Traveler@Example.COM");

        var result = await Login(controller, loginEmail);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task 정규화_이전에_대소문자_섞여_저장된_기존_계정도_로그인할_수_있다()
    {
        using var db = CreateDb();
        db.Users.Add(new User { Email = "Legacy.User@Example.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), Nickname = "legacy" });
        db.SaveChanges();

        var result = await Login(UserController(db), " legacy.user@EXAMPLE.com");

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task 이메일이_맞아도_비밀번호가_틀리면_로그인할_수_없다()
    {
        using var db = CreateDb();
        var controller = UserController(db);
        await Register(controller, "traveler@example.com");

        var result = await Login(controller, "TRAVELER@example.com", "wrong-password");

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task 대소문자와_공백만_다른_이메일로는_중복_가입할_수_없다()
    {
        using var db = CreateDb();
        var controller = UserController(db);
        await Register(controller, "traveler@example.com");

        var result = await Register(controller, " TRAVELER@Example.com ");

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task 여행_멤버_초대는_대소문자와_공백이_달라도_같은_사용자를_찾는다()
    {
        using var db = CreateDb();
        db.Users.Add(new User { Id = 1, Email = "owner@example.com", Nickname = "owner" });
        db.Users.Add(new User { Id = 2, Email = "Friend@Example.com", Nickname = "friend" });
        db.Trips.Add(new Trip { Id = 1, Title = "t", OwnerId = 1, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1) });
        db.TripMembers.Add(new TripMember { TripId = 1, UserId = 1 });
        db.SaveChanges();

        var controller = new TripController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test"))
                }
            }
        };

        var result = await controller.AddMember(1, new TripMemberAddDto { Email = "  FRIEND@example.COM " });

        Assert.IsType<OkObjectResult>(result);
        Assert.Contains(db.TripMembers, tm => tm.TripId == 1 && tm.UserId == 2);
    }
}
