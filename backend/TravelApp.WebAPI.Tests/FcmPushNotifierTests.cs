using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedData.Models;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services.Push;

namespace TravelApp.WebAPI.Tests;

// FcmPushNotifier의 "트립 멤버 -> 디바이스 토큰" 조회/제외 로직을 실제 Firebase 호출 없이 검증한다.
public class FcmPushNotifierTests
{
    private sealed class FakeFcmSender : IFcmSender
    {
        public List<(string Token, string Title, string Body)> Sent { get; } = new();
        public string? FailForToken { get; set; }

        public Task SendAsync(string deviceToken, string title, string body, CancellationToken ct = default)
        {
            if (deviceToken == FailForToken)
            {
                throw new InvalidOperationException("전송 실패 시뮬레이션");
            }
            Sent.Add((deviceToken, title, body));
            return Task.CompletedTask;
        }
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);
        db.Trips.Add(new Trip { Id = 1, Title = "제주 여행", OwnerId = 1 });
        db.TripMembers.Add(new TripMember { TripId = 1, UserId = 1 });
        db.TripMembers.Add(new TripMember { TripId = 1, UserId = 2 });
        db.TripMembers.Add(new TripMember { TripId = 1, UserId = 3 }); // 디바이스 토큰 미등록
        db.DeviceTokens.Add(new DeviceToken { UserId = 1, Token = "token-1" });
        db.DeviceTokens.Add(new DeviceToken { UserId = 2, Token = "token-2" });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task NotifyTripMembers_SendsToEveryMemberWithAToken()
    {
        var db = CreateDb();
        var sender = new FakeFcmSender();
        var notifier = new FcmPushNotifier(db, sender, NullLogger<FcmPushNotifier>.Instance);

        await notifier.NotifyTripMembersAsync(1, "제목", "내용");

        Assert.Equal(2, sender.Sent.Count);
        Assert.Contains(sender.Sent, s => s.Token == "token-1");
        Assert.Contains(sender.Sent, s => s.Token == "token-2");
    }

    [Fact]
    public async Task NotifyTripMembers_ExcludesGivenUser()
    {
        var db = CreateDb();
        var sender = new FakeFcmSender();
        var notifier = new FcmPushNotifier(db, sender, NullLogger<FcmPushNotifier>.Instance);

        await notifier.NotifyTripMembersAsync(1, "정산 결과 도착", "내용", excludeUserId: 1);

        var sent = Assert.Single(sender.Sent);
        Assert.Equal("token-2", sent.Token);
    }

    [Fact]
    public async Task NotifyTripMembers_OneFailure_DoesNotBlockOthers()
    {
        var db = CreateDb();
        var sender = new FakeFcmSender { FailForToken = "token-1" };
        var notifier = new FcmPushNotifier(db, sender, NullLogger<FcmPushNotifier>.Instance);

        await notifier.NotifyTripMembersAsync(1, "제목", "내용");

        var sent = Assert.Single(sender.Sent);
        Assert.Equal("token-2", sent.Token);
    }

    [Fact]
    public async Task NotifyTripMembers_UnknownTrip_SendsNothing()
    {
        var db = CreateDb();
        var sender = new FakeFcmSender();
        var notifier = new FcmPushNotifier(db, sender, NullLogger<FcmPushNotifier>.Instance);

        await notifier.NotifyTripMembersAsync(404, "제목", "내용");

        Assert.Empty(sender.Sent);
    }
}
