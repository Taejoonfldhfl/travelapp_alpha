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
}
