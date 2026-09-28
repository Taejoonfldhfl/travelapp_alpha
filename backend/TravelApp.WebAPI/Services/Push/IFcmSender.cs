namespace TravelApp.WebAPI.Services.Push;

// FCM 실제 발송을 감싸는 얇은 인터페이스. FcmPushNotifier의 트립 멤버/토큰 조회 로직을
// 실제 Firebase 호출 없이(이 인터페이스를 mock으로 바꿔서) 테스트할 수 있게 분리한다.
public interface IFcmSender
{
    Task SendAsync(string deviceToken, string title, string body, CancellationToken ct = default);
}
