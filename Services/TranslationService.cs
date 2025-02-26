using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class TranslationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly AppConfig _config;

    public TranslationService(string apiUrl = "http://192.168.100.10:1234")
    {
        _apiUrl = apiUrl;
        _httpClient = new HttpClient();
        _config = AppConfig.Load();
    }

    public bool IsEnabled => _config.TranslationEnabled;
    public string TargetLanguage => _config.TargetLanguage;

    public void ToggleTranslation()
    {
        _config.TranslationEnabled = !_config.TranslationEnabled;
        _config.Save();
        Console.WriteLine($"Translation is now {(_config.TranslationEnabled ? "enabled" : "disabled")}");
    }

    public void SetTargetLanguage(string language)
    {
        _config.TargetLanguage = language;
        _config.Save();
        Console.WriteLine($"Target language set to: {_config.TargetLanguage}");
    }

    public async Task<string> TranslateMessageAsync(string message)
    {
        if (!_config.TranslationEnabled) return message;

        try
        {
            var requestData = new
            {
                model = "default",
                messages = new[]
                {
                    new { role = "system", content = $"You are a translator for Twitch chat messages. Translate the following message from Spanish to {_config.TargetLanguage}. IMPORTANT: Provide ONLY the direct translation without adding ANY emojis, emoticons, or additional text. Preserve any original emotes or emoticons that were in the message. Keep the same formatting and capitalization style as the original message." },
                    new { role = "user", content = message }
                },
                temperature = 0.3,
                max_tokens = 500
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_apiUrl}/v1/chat/completions", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(responseBody);
                
                var translatedText = jsonDoc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();
                
                return translatedText ?? message;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Translation error: {ex.Message}");
        }

        return message;
    }
} 