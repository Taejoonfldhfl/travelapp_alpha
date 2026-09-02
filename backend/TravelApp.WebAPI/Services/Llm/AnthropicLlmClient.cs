using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TravelApp.WebAPI.Services.Llm
{
    // Anthropic Messages API(https://api.anthropic.com/v1/messages)를 HttpClient로 직접 호출하는 클라이언트.
    public class AnthropicLlmClient
    {
        private const string Endpoint = "https://api.anthropic.com/v1/messages";
        private const string AnthropicVersion = "2023-06-01";
        private const string DefaultModel = "claude-haiku-4-5-20251001";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public AnthropicLlmClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;

            _apiKey = configuration["Anthropic:ApiKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException(
                    "Anthropic:ApiKey가 설정되지 않았습니다. 다음 명령으로 설정한 뒤 다시 실행하세요:\n" +
                    "  dotnet user-secrets set \"Anthropic:ApiKey\" \"<YOUR_API_KEY>\" --project backend/TravelApp.WebAPI");
            }

            var configuredModel = configuration["Anthropic:Model"];
            _model = string.IsNullOrWhiteSpace(configuredModel) ? DefaultModel : configuredModel;
        }

        public async Task<string> SendAsync(
            string systemPrompt,
            IReadOnlyList<ChatHistoryMessage> history,
            CancellationToken cancellationToken = default)
        {
            var payload = new AnthropicRequest
            {
                Model = _model,
                MaxTokens = 2048,
                System = systemPrompt,
                Messages = history
                    .Select(m => new AnthropicRequestMessage { Role = m.Role, Content = m.Content })
                    .ToList()
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", AnthropicVersion);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Anthropic API 호출이 실패했습니다 ({(int)response.StatusCode} {response.StatusCode}): {body}");
            }

            AnthropicResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<AnthropicResponse>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Anthropic API 응답을 파싱하지 못했습니다: {ex.Message}");
            }

            var text = parsed?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Anthropic API 응답에 텍스트 콘텐츠가 없습니다.");
            }

            return text;
        }

        private class AnthropicRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("max_tokens")]
            public int MaxTokens { get; set; }

            [JsonPropertyName("system")]
            public string System { get; set; } = string.Empty;

            [JsonPropertyName("messages")]
            public List<AnthropicRequestMessage> Messages { get; set; } = new();
        }

        private class AnthropicRequestMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private class AnthropicResponse
        {
            [JsonPropertyName("content")]
            public List<AnthropicContentBlock>? Content { get; set; }
        }

        private class AnthropicContentBlock
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;

            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }
    }
}
