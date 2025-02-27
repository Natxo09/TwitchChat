using System;
using System.Threading.Tasks;
using DiscordRPC;
using DiscordRPC.Logging;

public class DiscordRichPresenceService : IDisposable
{
    private readonly DiscordRpcClient? _client;
    private readonly AppConfig _config;
    private bool _isInitialized = false;
    
    // Nombre de la imagen subida al portal de desarrolladores de Discord
    // El nombre debe coincidir exactamente con el nombre del archivo sin extensión
    private const string APP_ICON_KEY = "TwitchChat";
    private const string TRANSLATION_ICON_KEY = "translation";
    
    public DiscordRichPresenceService(AppConfig config)
    {
        _config = config;
        
        // Only create the client if Discord Rich Presence is enabled and we have an App ID
        if (_config.DiscordRichPresenceEnabled && !string.IsNullOrEmpty(_config.DiscordAppId))
        {
            _client = new DiscordRpcClient(_config.DiscordAppId)
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Warning }
            };
            
            _client.OnReady += (sender, e) =>
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("✅ ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Discord Rich Presence: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Connected to {e.User.Username}");
                Console.ForegroundColor = originalColor;
            };
            
            _client.OnError += (sender, e) =>
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("❌ ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Discord Rich Presence: ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error - {e.Message}");
                Console.ForegroundColor = originalColor;
            };
        }
    }
    
    public bool Initialize()
    {
        if (_isInitialized) return true;
        if (_client == null) return false;
        
        try
        {
            _isInitialized = _client.Initialize();
            if (_isInitialized)
            {
                // Registrar eventos adicionales para depuración
                _client.OnPresenceUpdate += (sender, e) => 
                {
                    //Console.WriteLine($"Discord Presence updated successfully");
                };
                
                UpdatePresence();
            }
            return _isInitialized;
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"Error initializing Discord Rich Presence: {ex.Message}");
            return false;
        }
    }
    
    public void UpdatePresence()
    {
        if (!_isInitialized || _client == null) return;
        
        try
        {
            // Crear mensajes más descriptivos e interesantes
            string details = $"📺 Watching {_config.TwitchChannel}'s chat";
            
            // Diferentes estados según si la traducción está activada o no
            string state = _config.TranslationEnabled 
                ? $"🌐 translating messages to {_config.TargetLanguage}" 
                : "🎮 Watching live chat";
                
            _client.SetPresence(new RichPresence()
            {
                Details = details,
                State = state,
                Assets = new Assets()
                {
                    // Usar el nombre exacto de la imagen subida (sin extensión)
                    LargeImageKey = APP_ICON_KEY,
                    LargeImageText = $"TwitchChat Translator - {_config.TwitchChannel}",
                    SmallImageKey = TRANSLATION_ICON_KEY,
                    SmallImageText = $"Breaking language barriers in Twitch chat"
                },
                Timestamps = new Timestamps()
                {
                    Start = DateTime.UtcNow
                },
                Buttons = new Button[]
                {
                    new Button() { Label = "🔴 Watch Stream", Url = $"https://twitch.tv/{_config.TwitchChannel}" },
                    new Button() { Label = "💻 GitHub Repository", Url = _config.GitHubRepoUrl }
                }
            });
            
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("🔄 ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Discord Rich Presence: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Updated with channel: {_config.TwitchChannel}");
            Console.ForegroundColor = originalColor;
        }
        catch (Exception ex)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("❌ ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Discord Rich Presence: ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error updating - {ex.Message}");
            Console.ForegroundColor = originalColor;
        }
    }
    
    public void UpdateChannel(string channel)
    {
        if (!_isInitialized) return;
        
        UpdatePresence();
    }
    
    public void UpdateLanguage(string language)
    {
        if (!_isInitialized) return;
        
        UpdatePresence();
    }
    
    // Método para depurar problemas con las imágenes
    public void LogAssetInfo()
    {
        if (!_isInitialized || _client == null) return;
        
        var originalColor = Console.ForegroundColor;
        
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("=== Discord Rich Presence Asset Info ===");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("- Application ID: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"{_config.DiscordAppId}");
        
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("- Large Image Key: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"{APP_ICON_KEY}");
        
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("- Small Image Key: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"{TRANSLATION_ICON_KEY}");
        
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Note: Image keys are case-sensitive and should not include file extensions");
        Console.WriteLine("Make sure you've uploaded these images in the Discord Developer Portal");
        
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("================================================");
        Console.WriteLine();
        
        Console.ForegroundColor = originalColor;
    }
    
    public void Dispose()
    {
        _client?.Dispose();
    }
} 