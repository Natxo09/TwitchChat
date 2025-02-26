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
            if (_config.DebugMode) WriteDebug($"Mensaje demasiado largo para traducir ({message.Length} caracteres)");
            return message;
        }
        
        // Verificar si el mensaje ya está en caché
        if (_translationCache.TryGetValue(message, out string? cachedTranslation))
        {
            if (_config.DebugMode) WriteDebug($"Usando traducción en caché");
            return cachedTranslation;
        }

        // Solo mostrar mensajes de depuración si está activado el modo debug
        if (_config.DebugMode) WriteDebug($"Traduciendo mensaje: {message}");
        
        try
        {
            if (_config.DebugMode)
                WriteDebug($"Traduciendo mensaje: {message}");
            
            // Verificar si el mensaje parece estar en inglés y el idioma destino es inglés
            if (_config.TargetLanguage == "English" && IsLikelyEnglish(message))
            {
                if (_config.DebugMode) WriteDebug($"El mensaje parece estar ya en inglés, omitiendo traducción");
                return message;
            }
            
            // Asegurarse de que los mensajes cortos también se traducen
            if (message.Length < 5 && !ContainsOnlyEmotesOrCommands(message))
            {
                if (_config.DebugMode) WriteDebug($"Mensaje corto, forzando traducción: {message}");
            }
            
            // Prompt más estricto para asegurar traducción literal
            var requestData = new
            {
                model = "default",
                messages = new[]
                {
                    new { role = "system", content = $"TAREA: Traduce este mensaje de Twitch al {_config.TargetLanguage}. REGLAS: 1) Mantén emotes, comandos, hashtags y @menciones exactamente igual. 2) NO traduzcas nombres propios, marcas o términos técnicos. 3) Si el mensaje ya está en {_config.TargetLanguage}, devuélvelo sin cambios. 4) Traduce incluso mensajes cortos o slang. 5) NO añadas NADA que no esté en el original (ni emojis, ni puntuación extra). IMPORTANTE: Tu respuesta debe contener SOLO la traducción, nada más." },
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
                    // Verificar que la traducción no sea idéntica al mensaje original
                    // cuando sabemos que debería ser diferente (idiomas distintos)
                    if (translatedText == message && 
                        !IsLikelyAlreadyInTargetLanguage(message, _config.TargetLanguage) &&
                        !ContainsOnlyEmotesOrCommands(message))
                    {
                        if (_config.DebugMode)
                            WriteDebug($"La traducción es idéntica al original, posible error");
                        // Intentar una segunda vez con un prompt más directo
                        return await RetryTranslationAsync(message);
                    }
                    
                    if (_config.DebugMode)
                        WriteDebug($"Traducción exitosa: {translatedText}");
                    
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
                else
                {
                    if (_config.DebugMode)
                        WriteDebug($"La respuesta no contiene texto traducido");
                }
            }
            else
            {
                if (_config.DebugMode)
                    WriteDebug($"Error en la respuesta HTTP: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            // Reducir el registro de errores para no saturar la consola
            if (ex is not TaskCanceledException && ex is not HttpRequestException)
            {
                WriteError($"Translation error: {ex.Message}");
            }
            else if (_config.DebugMode)
            {
                WriteDebug($"Error de red o timeout: {ex.GetType().Name}");
            }
        }

        return message;
    }
    
    private async Task<string> RetryTranslationAsync(string message)
    {
        try
        {
            if (_config.DebugMode) WriteDebug($"Reintentando traducción con prompt simplificado");
            
            // Prompt extremadamente directo para forzar la traducción
            var requestData = new
            {
                model = "default",
                messages = new[]
                {
                    new { role = "system", content = $"Traduce este texto exactamente al {_config.TargetLanguage}, incluso si contiene nombres propios o términos técnicos. No omitas ninguna parte. Responde SOLO con la traducción." },
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
                    if (_config.DebugMode)
                        WriteDebug($"Segundo intento exitoso: {translatedText}");
                    return translatedText;
                }
            }
        }
        catch (Exception ex)
        {
            if (_config.DebugMode) WriteDebug($"Error en segundo intento: {ex.Message}");
        }
        
        return message;
    }
    
    // Método para verificar si un mensaje contiene solo emotes o comandos
    private bool ContainsOnlyEmotesOrCommands(string message)
    {
        // Lista de emotes comunes de Twitch
        string[] commonEmotes = { "Kappa", "PogChamp", "LUL", "BibleThump", "DansGame", "Jebaited", 
                                 "KEKW", "monkaS", "PepeHands", "Pog", "OMEGALUL", "ResidentSleeper",
                                 "TriHard", "4Head", "BBoomer", "gnomePls" };
                                 
        // Eliminar espacios
        string trimmed = message.Trim();
        
        // Verificar si es un comando
        if (trimmed.StartsWith("!")) return true;
        
        // Verificar si solo contiene emotes conocidos
        string[] words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return true;
        
        foreach (var word in words)
        {
            // Si alguna palabra no es un emote conocido y no es un comando
            if (!commonEmotes.Contains(word) && !word.StartsWith("!") && !word.StartsWith("@"))
            {
                return false;
            }
        }
        
        return true;
    }
    
    // Método para verificar si un mensaje parece estar en inglés
    private bool IsLikelyEnglish(string message)
    {
        // Palabras comunes en inglés
        string[] commonEnglishWords = { "the", "and", "is", "in", "to", "you", "that", "it", "he", "was", 
                                      "for", "on", "are", "as", "with", "his", "they", "at", "be", "this", 
                                      "have", "from", "or", "one", "had", "by", "but", "not", "what", "all", 
                                      "were", "we", "when", "your", "can", "said", "there", "use", "an", "each", 
                                      "which", "she", "do", "how", "their", "if", "will", "up", "other", "about", 
                                      "out", "many", "then", "them", "these", "so", "some", "her", "would", "make", 
                                      "like", "him", "into", "time", "has", "look", "two", "more", "go", "see" };
        
        // Dividir el mensaje en palabras
        string[] words = message.ToLower().Split(new[] { ' ', ',', '.', '!', '?', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Si no hay palabras, no podemos determinar
        if (words.Length == 0) return false;
        
        // Contar palabras en inglés
        int englishWordCount = words.Count(word => commonEnglishWords.Contains(word));
        
        // Si más del 40% de las palabras son inglesas, probablemente es inglés
        return (double)englishWordCount / words.Length >= 0.4;
    }
    
    // Método simple para estimar si un mensaje ya está en el idioma de destino
    private bool IsLikelyAlreadyInTargetLanguage(string message, string targetLanguage)
    {
        // Esta es una implementación muy básica que podría mejorarse
        // con una biblioteca de detección de idiomas
        
        // Si el mensaje solo contiene emotes, comandos o menciones, asumimos que no necesita traducción
        if (message.All(c => !char.IsLetter(c) || char.IsDigit(c) || c == '@' || c == '!'))
            return true;
            
        // Palabras comunes en diferentes idiomas para una detección básica
        Dictionary<string, string[]> commonWords = new()
        {
            {"English", new[] {"the", "and", "is", "in", "to", "you", "that", "it", "he", "was", "for", "on", "are", "as", "with", "his", "they", "at"}},
            {"Spanish", new[] {"el", "la", "los", "las", "un", "una", "y", "es", "en", "que", "de", "por", "con", "para", "su", "se", "lo", "como"}},
            {"French", new[] {"le", "la", "les", "un", "une", "et", "est", "en", "que", "qui", "dans", "pour", "avec", "sur", "ce", "il", "elle", "je"}},
            {"German", new[] {"der", "die", "das", "ein", "eine", "und", "ist", "in", "zu", "den", "dem", "mit", "für", "auf", "sie", "er", "es", "ich"}},
            {"Italian", new[] {"il", "la", "i", "le", "un", "una", "e", "è", "in", "che", "di", "per", "con", "su", "questo", "lui", "lei", "io"}},
            {"Portuguese", new[] {"o", "a", "os", "as", "um", "uma", "e", "é", "em", "que", "de", "para", "com", "no", "na", "ele", "ela", "eu"}},
            // Para idiomas como japonés, coreano, chino y ruso, este enfoque simple no funcionaría bien
        };
        
        if (commonWords.TryGetValue(targetLanguage, out var words))
        {
            // Dividir el mensaje en palabras
            var messageWords = message.ToLower().Split(new[] {' ', ',', '.', '!', '?', ':', ';'}, StringSplitOptions.RemoveEmptyEntries);
            
            // Contar cuántas palabras comunes del idioma de destino aparecen en el mensaje
            int matchCount = messageWords.Count(word => words.Contains(word));
            
            // Si hay suficientes coincidencias, es probable que ya esté en el idioma de destino
            return matchCount >= Math.Min(2, messageWords.Length / 3);
        }
        
        return false;
    }

    // Método para escribir mensajes de depuración en color
    private void WriteDebug(string message)
    {
        lock (Console.Out) // Evitar que otros hilos escriban en la consola al mismo tiempo
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[DEBUG] {message}");
            Console.ForegroundColor = originalColor;
        }
    }
    
    // Método para escribir errores en color
    private void WriteError(string message)
    {
        lock (Console.Out) // Evitar que otros hilos escriban en la consola al mismo tiempo
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {message}");
            Console.ForegroundColor = originalColor;
        }
    }

    public void UpdateTargetLanguage(string targetLanguage)
    {
        // Limpiar la caché cuando cambia el idioma objetivo
        _translationCache.Clear();
        
        // Mostrar siempre este mensaje, incluso sin modo debug
        Console.WriteLine($"[INFO] Idioma objetivo actualizado a: {targetLanguage}");
    }
} 