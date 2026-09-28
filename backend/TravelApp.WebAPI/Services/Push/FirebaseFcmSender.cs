using FirebaseAdmin.Messaging;

namespace TravelApp.WebAPI.Services.Push;

// FirebaseApp이 이미 초기화되어 있다고 가정한다(초기화는 Program.cs에서 서비스 계정 키가
// 확인됐을 때만 수행). 이 클래스는 실제 FCM 서버로 나가는 유일한 지점이라 테스트에서는
// IFcmSender를 mock으로 바꿔 대체한다.
public class FirebaseFcmSender : IFcmSender
{
    public async Task SendAsync(string deviceToken, string title, string body, CancellationToken ct = default)
    {
        // FirebaseAdmin 3.7.0부터 Message.Token은 폐기(deprecated)되어 Fid(등록 토큰)를 대신 사용한다.
        var message = new Message
        {
            Fid = deviceToken,
            Notification = new Notification { Title = title, Body = body }
        };

        await FirebaseMessaging.DefaultInstance.SendAsync(message, ct);
    }
}
