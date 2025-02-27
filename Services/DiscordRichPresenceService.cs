using System;
using System.Threading.Tasks;
using DiscordRPC;
using DiscordRPC.Logging;

public class DiscordRichPresenceService : IDisposable
{
    private readonly DiscordRpcClient? _client;
    private readonly AppConfig _config;
    private bool _isInitialized = false;
    
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
                Console.WriteLine($"Discord Rich Presence connected to {e.User.Username}");
            };
            
            _client.OnError += (sender, e) =>
            {
                Console.WriteLine($"Discord Rich Presence error: {e.Message}");
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
                UpdatePresence();
            }
            return _isInitialized;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing Discord Rich Presence: {ex.Message}");
            return false;
        }
    }
    
    public void UpdatePresence()
    {
        if (!_isInitialized || _client == null) return;
        
        try
        {
            _client.SetPresence(new RichPresence()
            {
                Details = $"Watching Twitch Chat: {_config.TwitchChannel}",
                State = $"Translating to {_config.TargetLanguage}",
                Assets = new Assets()
                {
                    LargeImageKey = "app_icon", // Upload this asset in your Discord Developer Portal
                    LargeImageText = "TwitchChat Translator",
                    SmallImageKey = "translation_icon", // Upload this asset in your Discord Developer Portal
                    SmallImageText = $"Translating to {_config.TargetLanguage}"
                },
                Timestamps = new Timestamps()
                {
                    Start = DateTime.UtcNow
                },
                Buttons = new Button[]
                {
                    new Button() { Label = "Watch Stream", Url = $"https://twitch.tv/{_config.TwitchChannel}" },
                    new Button() { Label = "GitHub Repository", Url = _config.GitHubRepoUrl }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating Discord Rich Presence: {ex.Message}");
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
    
    public void Dispose()
    {
        _client?.Dispose();
    }
} 