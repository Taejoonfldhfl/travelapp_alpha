namespace TravelApp.WebAPI.Services.Llm
{
    // 앱이 실제로 기동될 때 Anthropic:ApiKey 설정을 검증한다.
    // AnthropicLlmClient의 생성자가 키 누락 시 예외를 던지므로, 여기서 강제로 한 번 생성해
    // 첫 요청이 올 때까지 기다리지 않고 시작 시점에 바로 실패하게 만든다.
    // IHostedService로 구현한 이유: dotnet ef 같은 EF Core 디자인타임 도구는 서비스 컨테이너만
    // 구성하고 IHostedService.StartAsync는 실행하지 않으므로, 마이그레이션 작업이
    // API 키 미설정 때문에 실패하지 않는다.
    public class AnthropicStartupValidator : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public AnthropicStartupValidator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            scope.ServiceProvider.GetRequiredService<AnthropicLlmClient>();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
