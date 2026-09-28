using Microsoft.EntityFrameworkCore;
using TravelApp.WebAPI.Data;
using TravelApp.WebAPI.Services.Expenses;

namespace TravelApp.WebAPI.Services.Push;

// 실제 기기 푸시(FCM)로 여행 멤버에게 알림을 보낸다. 토큰이 없는 멤버(디바이스 미등록)는
// 조용히 건너뛰고, 개별 발송 실패가 다른 멤버 발송을 막지 않는다.
public class FcmPushNotifier : IPushNotifier
{
    private readonly ApplicationDbContext _context;
    private readonly IFcmSender _sender;
    private readonly ILogger<FcmPushNotifier> _logger;

    public FcmPushNotifier(ApplicationDbContext context, IFcmSender sender, ILogger<FcmPushNotifier> logger)
    {
        _context = context;
        _sender = sender;
        _logger = logger;
    }

    public async Task NotifyTripMembersAsync(int tripId, string title, string body, int? excludeUserId = null, CancellationToken ct = default)
    {
        var tokens = await _context.TripMembers
            .Where(m => m.TripId == tripId && (excludeUserId == null || m.UserId != excludeUserId))
            .Join(_context.DeviceTokens, m => m.UserId, d => d.UserId, (m, d) => d.Token)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            try
            {
                await _sender.SendAsync(token, title, body, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FCM 발송 실패 (trip={TripId})", tripId);
            }
        }
    }
}
