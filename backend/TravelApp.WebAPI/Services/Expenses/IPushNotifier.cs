namespace TravelApp.WebAPI.Services.Expenses;

// 다른 멤버가 지출을 추가해 예산이 초과된 경우 등, 서버발 푸시가 필요한 지점을 위한 인터페이스.
// 실제 FCM 연동은 미룬다. 현재는 로그만 남기는 Mock 만 등록한다.
public interface IPushNotifier
{
    Task NotifyTripMembersAsync(int tripId, string title, string body, CancellationToken ct = default);
}

public class LogPushNotifier : IPushNotifier
{
    private readonly ILogger<LogPushNotifier> _logger;

    public LogPushNotifier(ILogger<LogPushNotifier> logger)
    {
        _logger = logger;
    }

    public Task NotifyTripMembersAsync(int tripId, string title, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("[MockPush] trip={TripId} title={Title} body={Body}", tripId, title, body);
        return Task.CompletedTask;
    }
}
