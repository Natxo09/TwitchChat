// See https://aka.ms/new-console-template for more information

using System.Net.Sockets;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

class Program
{
    private static readonly BadgeService _badgeService = new();
    private static readonly SubMessageHandler _subHandler = new();
    private static readonly BitsMessageHandler _bitsHandler = new();
    private static readonly TranslationService _translationService = new();
    private static readonly TranslatedMessageHandler _translatedMessageHandler = new(_translationService, _badgeService);
    private static readonly AppConfig _config = AppConfig.Load();

    private static readonly List<string> _availableLanguages = new()
    {
        "English", "Spanish", "French", "German", "Italian", 
        "Portuguese", "Japanese", "Korean", "Chinese", "Russian"
    };

    public static ConsoleColor GetUserColor(string username)
    {
        ConsoleColor[] colors =
        {
            ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Yellow,
            ConsoleColor.Blue, ConsoleColor.Magenta, ConsoleColor.Red
        };
        int hash = username.GetHashCode();
        return colors[Math.Abs(hash % colors.Length)];
    }
    
    static async Task Main(string[] args)
    {
        // Override channel from command line if provided
        if (args.Length > 0)
        {
            _config.TwitchChannel = args[0].ToLower();
            _config.Save();
        }
        
        // Show configuration options first
        if (ShowConfigMenu())
        {
            // User chose to exit without connecting
            return;
        }
        
        Console.Clear();
        Console.WriteLine($"Starting Twitch Chat with Spanish to {_config.TargetLanguage} Translation...");
        Console.WriteLine("Press 'T' to toggle translation on/off");
        Console.WriteLine("Press 'Q' to quit the application");
        
        // Start a task to listen for key presses
        _ = Task.Run(() => {
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.T)
                {
                    _translationService.ToggleTranslation();
                }
                else if (key.Key == ConsoleKey.Q)
                {
                    Console.WriteLine("Exiting application...");
                    Environment.Exit(0);
                }
            }
        });
        
        using TcpClient client = new TcpClient();
        await client.ConnectAsync("irc.chat.twitch.tv", 6667);
        
        using StreamReader reader = new StreamReader(client.GetStream());
        using StreamWriter writer = new StreamWriter(client.GetStream()) {AutoFlush = true};

        await writer.WriteLineAsync("CAP REQ :twitch.tv/tags twitch.tv/commands twitch.tv/membership");
        await writer.WriteLineAsync("NICK justinfan12345");
        await writer.WriteLineAsync("USER justinfan12345 8 * :justinfan12345");
        await writer.WriteLineAsync($"JOIN #{_config.TwitchChannel}");
        
        Console.WriteLine($"Connected to channel: {_config.TwitchChannel}");
        Console.WriteLine($"Showing chat with Spanish to {_config.TargetLanguage} translation (CTRL+C to exit...)");

        while (true)
        {
            string? message = await reader.ReadLineAsync();
            if (message == null) break;

            if (message.StartsWith("PING"))
            {
                await writer.WriteLineAsync("PONG :tmi.twitch.tv");
                continue;
            }

            if (message.Contains("USERNOTICE") && message.Contains("msg-id="))
            {
                _subHandler.HandleMessage(message);
                continue;
            }

            if (message.Contains("PRIVMSG"))
            {
                try
                {
                    string username = message.Contains("display-name=") 
                        ? message.Split("display-name=")[1].Split(";")[0]
                        : message.Substring(1, message.IndexOf("!") - 1);

                    if (string.IsNullOrEmpty(username)) continue;

                    if (_bitsHandler.HandleMessage(message, username)) continue;

                    // Use the translated message handler instead of direct console output
                    await _translatedMessageHandler.HandleMessageAsync(message, username);
                }
                catch
                {
                    continue;
                }
            }
        }
    }
    
    private static bool ShowConfigMenu()
    {
        Console.WriteLine("=== Twitch Chat Translator ===");
        Console.WriteLine();
        Console.WriteLine("Current Configuration:");
        Console.WriteLine($"- Twitch Channel: {_config.TwitchChannel}");
        Console.WriteLine($"- Target Language: {_config.TargetLanguage}");
        Console.WriteLine($"- Translation Enabled: {_config.TranslationEnabled}");
        Console.WriteLine();
        Console.WriteLine("Do you want to change the configuration?");
        Console.WriteLine("1. Yes, configure now");
        Console.WriteLine("2. No, continue with current settings");
        Console.WriteLine("3. Exit");
        Console.WriteLine();
        Console.Write("Select an option (1-3): ");
        
        while (true)
        {
            var key = Console.ReadKey(true);
            Console.WriteLine(key.KeyChar);
            
            switch (key.KeyChar)
            {
                case '1':
                    ConfigureApp();
                    return false; // Continue to chat
                    
                case '2':
                    return false; // Continue to chat with current settings
                    
                case '3':
                    return true; // Exit
                    
                default:
                    Console.Write("Invalid option. Select an option (1-3): ");
                    break;
            }
        }
    }
    
    private static void ConfigureApp()
    {
        bool exit = false;
        
        while (!exit)
        {
            Console.Clear();
            Console.WriteLine("=== Twitch Chat Translator Configuration ===");
            Console.WriteLine();
            Console.WriteLine($"1. Twitch Channel: {_config.TwitchChannel}");
            Console.WriteLine($"2. Target Language: {_config.TargetLanguage}");
            Console.WriteLine($"3. Translation Enabled: {_config.TranslationEnabled}");
            Console.WriteLine("4. Save and Continue");
            Console.WriteLine();
            Console.Write("Select an option (1-4): ");
            
            var key = Console.ReadKey(true);
            Console.WriteLine(key.KeyChar);
            
            switch (key.KeyChar)
            {
                case '1':
                    Console.Write("Enter new Twitch channel name: ");
                    string? channel = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(channel))
                    {
                        _config.TwitchChannel = channel.ToLower();
                    }
                    break;
                    
                case '2':
                    ShowLanguageOptions();
                    Console.Write("Select a language (1-10): ");
                    if (int.TryParse(Console.ReadLine(), out int langIndex) && 
                        langIndex >= 1 && langIndex <= _availableLanguages.Count)
                    {
                        _config.TargetLanguage = _availableLanguages[langIndex - 1];
                    }
                    break;
                    
                case '3':
                    _config.TranslationEnabled = !_config.TranslationEnabled;
                    break;
                    
                case '4':
                    _config.Save();
                    exit = true;
                    break;
            }
        }
    }
    
    private static void ShowLanguageOptions()
    {
        Console.WriteLine("\nAvailable Languages:");
        for (int i = 0; i < _availableLanguages.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {_availableLanguages[i]}");
        }
        Console.WriteLine();
    }
}