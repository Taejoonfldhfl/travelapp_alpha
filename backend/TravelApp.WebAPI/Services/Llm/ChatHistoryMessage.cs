namespace TravelApp.WebAPI.Services.Llm
{
    // ChatSession.MessagesJson에 저장되는 대화 한 턴. Anthropic Messages API의 role/content와 1:1로 대응한다.
    public class ChatHistoryMessage
    {
        public string Role { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }
}
