using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SharedData.DTOs;
using SharedData.Models;
using TravelApp.WebAPI.Controllers;
using TravelApp.WebAPI.Data;

namespace TravelApp.WebAPI.Tests;

// FCM 디바이스 토큰 등록(POST api/User/device-token): 사용자당 최신 토큰 1개만 유지(upsert).
public class UserControllerTests
{
    private static (UserController controller, ApplicationDbContext db) Create(int currentUserId = 1)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);
        db.Users.Add(new User { Id = currentUserId, Email = "u@test.com", PasswordHash = "x", Nickname = "테스터" });
        db.SaveChanges();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var controller = new UserController(db, configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()) }, "test"))
                }
            }
        };
        return (controller, db);
    }

    [Fact]
    public async Task UpsertDeviceToken_NewUser_CreatesRow()
    {
        var (controller, db) = Create();

        var result = await controller.UpsertDeviceToken(new DeviceTokenUpsertDto { Token = "token-1" });

        Assert.IsType<NoContentResult>(result);
        var row = Assert.Single(db.DeviceTokens);
        Assert.Equal(1, row.UserId);
        Assert.Equal("token-1", row.Token);
    }

    [Fact]
    public async Task UpsertDeviceToken_ExistingUser_OverwritesOldToken()
    {
        var (controller, db) = Create();
        await controller.UpsertDeviceToken(new DeviceTokenUpsertDto { Token = "old-token" });

        var result = await controller.UpsertDeviceToken(new DeviceTokenUpsertDto { Token = "new-token" });

        Assert.IsType<NoContentResult>(result);
        var row = Assert.Single(db.DeviceTokens);
        Assert.Equal("new-token", row.Token);
    }

    [Fact]
    public async Task UpsertDeviceToken_BlankToken_IsBadRequest()
    {
        var (controller, _) = Create();

        var result = await controller.UpsertDeviceToken(new DeviceTokenUpsertDto { Token = "  " });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static UserController CreateFor(ApplicationDbContext db, int userId, string email)
    {
        db.Users.Add(new User { Id = userId, Email = email, PasswordHash = "x", Nickname = "테스터" + userId });
        db.SaveChanges();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new UserController(db, configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
                }
            }
        };
    }

    // 같은 기기에서 다른 계정으로 로그인해 같은 FCM 토큰을 등록하면, 이전 계정 쪽 행은
    // 지워지고 새 계정으로 옮겨가야 한다(그렇지 않으면 이전 계정도 이 기기 알림을 계속 받는다).
    [Fact]
    public async Task UpsertDeviceToken_SameTokenRegisteredByAnotherAccount_MovesTokenToNewAccount()
    {
        var (controller1, db) = Create(currentUserId: 1);
        await controller1.UpsertDeviceToken(new DeviceTokenUpsertDto { Token = "shared-token" });

        var controller2 = CreateFor(db, userId: 2, email: "u2@test.com");
        var result = await controller2.UpsertDeviceToken(new DeviceTokenUpsertDto { Token = "shared-token" });

        Assert.IsType<NoContentResult>(result);
        var row = Assert.Single(db.DeviceTokens);
        Assert.Equal(2, row.UserId);
        Assert.Equal("shared-token", row.Token);
    }

    [Fact]
    public async Task DeleteDeviceToken_ExistingToken_RemovesRow()
    {
        var (controller, db) = Create();
        await controller.UpsertDeviceToken(new DeviceTokenUpsertDto { Token = "token-1" });

        var result = await controller.DeleteDeviceToken();

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.DeviceTokens);
    }

    [Fact]
    public async Task DeleteDeviceToken_NoTokenRegistered_StillReturnsNoContent()
    {
        var (controller, _) = Create();

        var result = await controller.DeleteDeviceToken();

        Assert.IsType<NoContentResult>(result);
    }
}
