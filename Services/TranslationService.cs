using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;

public class TranslationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly AppConfig _config;
    
    // Caché para mensajes ya traducidos
    private readonly ConcurrentDictionary<string, string> _translationCache = new();

    public TranslationService(string apiUrl = "http://192.168.100.10:1234")
    {
        _apiUrl = apiUrl;
        _httpClient = new HttpClient();
        _config = AppConfig.Load();
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
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
        if (_config.TargetLanguage != language)
        {
            _config.TargetLanguage = language;
            _config.Save();
            Console.WriteLine($"Target language set to: {_config.TargetLanguage}");
            
            // Limpiar caché al cambiar de idioma
            _translationCache.Clear();
        }
    }

    public async Task<string> TranslateMessageAsync(string message)
    {
        if (!_config.TranslationEnabled) return message;
        
        // No traducir mensajes muy largos para evitar ralentizar el chat
        if (message.Length > _config.MaxMessageLength)
        {
            return message;
        }
        
        // Verificar si el mensaje ya está en caché
        if (_translationCache.TryGetValue(message, out string? cachedTranslation))
        {
            return cachedTranslation;
        }

        try
        {
            // Prompt más estricto para asegurar traducción literal
            var requestData = new
            {
                model = "default",
                messages = new[]
                {
                    new { role = "system", content = $"INSTRUCCIONES ESTRICTAS: Detecta automáticamente el idioma del mensaje y tradúcelo a {_config.TargetLanguage}. Estás traduciendo mensajes de chat de Twitch que contienen jerga de videojuegos, emotes, memes y expresiones coloquiales. SOLO debes traducir el texto EXACTAMENTE como está, sin añadir NADA. NO añadas emojis, emoticones, símbolos, puntuación extra o cualquier otro elemento que no esté en el mensaje original. Mantén EXACTAMENTE los mismos emotes de Twitch (como Kappa, PogChamp, LUL, etc.), comandos (como !uptime, !commands), hashtags y menciones (@usuario) que aparezcan en el mensaje original. NO traduzcas nombres propios, nombres de juegos, o términos técnicos específicos. Respeta mayúsculas/minúsculas del original cuando sea posible. Tu respuesta debe contener ÚNICAMENTE la traducción literal. Si el mensaje ya está en {_config.TargetLanguage}, devuélvelo exactamente igual." },
                    new { role = "user", content = message }
                },
                temperature = 0.1,
                max_tokens = 200
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
                
                if (!string.IsNullOrEmpty(translatedText))
                {
                    // Guardar en caché para futuras traducciones
                    _translationCache.TryAdd(message, translatedText);
                    
                    // Limitar el tamaño de la caché
                    if (_translationCache.Count > _config.CacheSize)
                    {
                        // Eliminar una entrada aleatoria (simplificado)
                        var firstKey = _translationCache.Keys.FirstOrDefault();
                        if (firstKey != null)
                        {
                            _translationCache.TryRemove(firstKey, out _);
                        }
                    }
                    
                    return translatedText;
                }
            }
        }
        catch (Exception ex)
        {
            // Reducir el registro de errores para no saturar la consola
            if (ex is not TaskCanceledException && ex is not HttpRequestException)
            {
                Console.WriteLine($"Translation error: {ex.Message}");
            }
        }

        return message;
    }
} 