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

    // Colores para el menú
    private static readonly ConsoleColor TitleColor = ConsoleColor.Cyan;
    private static readonly ConsoleColor HeaderColor = ConsoleColor.Magenta;
    private static readonly ConsoleColor OptionColor = ConsoleColor.Yellow;
    private static readonly ConsoleColor ValueColor = ConsoleColor.Green;
    private static readonly ConsoleColor HighlightColor = ConsoleColor.White;

    // ASCII Art para el logo
    private static readonly string[] LogoArt = new string[]
    {
        @"  _______       _ _       _       _____ _           _   ",
        @" |__   __|     (_) |     | |     / ____| |         | |  ",
        @"    | |_      ___| |_ ___| |__  | |    | |__   __ _| |_ ",
        @"    | \ \ /\ / / | __/ __| '_ \ | |    | '_ \ / _` | __|",
        @"    | || V  V /| | || (__| | | || |____| | | | (_| | |_ ",
        @"    |_| \_/\_/ |_|\__\___|_| |_| \_____|_| |_|\__,_|\__|",
        @"                                                         ",
        @"              T R A N S L A T O R                        "
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
        PrintColorText($"Starting Twitch Chat with Auto-Detect to {_config.TargetLanguage} Translation...", HeaderColor);
        PrintColorText("Press 'T' to toggle translation on/off", OptionColor);
        PrintColorText("Press 'Q' to quit the application", OptionColor);
        
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
        Console.WriteLine($"Showing chat with automatic language detection and translation to {_config.TargetLanguage} (CTRL+C to exit...)");

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
                    _translatedMessageHandler.HandleMessage(message, username);
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
        Console.Clear();
        
        // Mostrar el logo ASCII
        foreach (var line in LogoArt)
        {
            PrintColorText(line, TitleColor);
        }
        
        Console.WriteLine();
        PrintColorText("=== Twitch Chat Translator ===", HeaderColor);
        Console.WriteLine();
        
        PrintColorText("Current Configuration:", HighlightColor);
        PrintConfigItem("Twitch Channel", _config.TwitchChannel);
        PrintConfigItem("Target Language", _config.TargetLanguage);
        PrintConfigItem("Translation Enabled", _config.TranslationEnabled.ToString());
        PrintConfigItem("Performance Settings", $"Max Length {_config.MaxMessageLength} chars, Cache Size {_config.CacheSize}");
        PrintConfigItem("Debug Mode", _config.DebugMode.ToString());
        
        Console.WriteLine();
        PrintColorText("Do you want to change the configuration?", HighlightColor);
        PrintMenuOption("1", "Yes, configure now");
        PrintMenuOption("2", "No, continue with current settings");
        PrintMenuOption("3", "Exit");
        
        Console.WriteLine();
        PrintColorText("Select an option (1-3): ", OptionColor);
        
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
                    PrintColorText("Invalid option. Select an option (1-3): ", OptionColor);
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
            
            // Mostrar un logo más pequeño en la pantalla de configuración
            PrintColorText(@"  _______       _ _       _       _____ _           _   ", TitleColor);
            PrintColorText(@" |__   __|     (_) |     | |     / ____| |         | |  ", TitleColor);
            PrintColorText(@"    | | _ __ __ _ _ _ __ | |__ _| |    | |__   __ _| |_ ", TitleColor);
            PrintColorText(@"    | || '__/ _` | | '_ \| __/ _` |    | '_ \ / _` | __|", TitleColor);
            PrintColorText(@"    | || | | (_| | | | | | || (_| |____| | | | (_| | |_ ", TitleColor);
            PrintColorText(@"    |_||_|  \__,_|_|_| |_|\__\__,_\_____|_| |_|\__,_|\__|", TitleColor);
            Console.WriteLine();
            
            PrintColorText("=== Twitch Chat Translator Configuration ===", HeaderColor);
            Console.WriteLine();
            
            PrintMenuOption("1", $"Twitch Channel: {_config.TwitchChannel}");
            PrintMenuOption("2", $"Target Language: {_config.TargetLanguage}");
            PrintMenuOption("3", $"Translation Enabled: {_config.TranslationEnabled}");
            PrintMenuOption("4", "Performance Settings");
            PrintMenuOption("5", $"Debug Mode: {_config.DebugMode}");
            PrintMenuOption("6", "Save and Continue");
            
            Console.WriteLine();
            PrintColorText("Select an option (1-6): ", OptionColor);
            
            var key = Console.ReadKey(true);
            Console.WriteLine(key.KeyChar);
            
            switch (key.KeyChar)
            {
                case '1':
                    PrintColorText("Enter new Twitch channel name: ", OptionColor);
                    string? channel = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(channel))
                    {
                        _config.TwitchChannel = channel.ToLower();
                    }
                    break;
                    
                case '2':
                    ShowLanguageOptions();
                    PrintColorText("Select a language (1-10): ", OptionColor);
                    if (int.TryParse(Console.ReadLine(), out int langIndex) && 
                        langIndex >= 1 && langIndex <= _availableLanguages.Count)
                    {
                        _config.TargetLanguage = _availableLanguages[langIndex - 1];
                    }
                    break;
                    
                case '3':
                    _config.TranslationEnabled = !_config.TranslationEnabled;
                    PrintColorText($"Translation is now {(_config.TranslationEnabled ? "enabled" : "disabled")}", ValueColor);
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    break;
                    
                case '4':
                    ConfigurePerformance();
                    break;
                    
                case '5':
                    _config.DebugMode = !_config.DebugMode;
                    PrintColorText($"Debug mode is now {(_config.DebugMode ? "enabled" : "disabled")}", ValueColor);
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    break;
                    
                case '6':
                    _config.Save();
                    exit = true;
                    break;
            }
        }
    }
    
    private static void ConfigurePerformance()
    {
        Console.Clear();
        PrintColorText("=== Performance Settings ===", HeaderColor);
        Console.WriteLine();
        
        PrintMenuOption("1", "Max Message Length (characters to translate)");
        PrintColorText($"   Current: {_config.MaxMessageLength} chars", ValueColor);
        
        PrintMenuOption("2", "Cache Size (number of messages to cache)");
        PrintColorText($"   Current: {_config.CacheSize} messages", ValueColor);
        
        PrintMenuOption("3", "Timeout (seconds to wait for translation)");
        PrintColorText($"   Current: {_config.TimeoutSeconds} seconds", ValueColor);
        
        PrintMenuOption("4", "Back to main menu");
        
        Console.WriteLine();
        PrintColorText("Select an option (1-4): ", OptionColor);
        
        var key = Console.ReadKey(true);
        Console.WriteLine(key.KeyChar);
        
        switch (key.KeyChar)
        {
            case '1':
                PrintColorText("Enter max message length (50-500): ", OptionColor);
                if (int.TryParse(Console.ReadLine(), out int maxLength) && 
                    maxLength >= 50 && maxLength <= 500)
                {
                    _config.MaxMessageLength = maxLength;
                }
                break;
                
            case '2':
                PrintColorText("Enter cache size (10-1000): ", OptionColor);
                if (int.TryParse(Console.ReadLine(), out int cacheSize) && 
                    cacheSize >= 10 && cacheSize <= 1000)
                {
                    _config.CacheSize = cacheSize;
                }
                break;
                
            case '3':
                PrintColorText("Enter timeout in seconds (1-30): ", OptionColor);
                if (int.TryParse(Console.ReadLine(), out int timeout) && 
                    timeout >= 1 && timeout <= 30)
                {
                    _config.TimeoutSeconds = timeout;
                }
                break;
                
            case '4':
                // Just return to main menu
                break;
        }
    }
    
    private static void ShowLanguageOptions()
    {
        Console.WriteLine("\nAvailable Languages:");
        for (int i = 0; i < _availableLanguages.Count; i++)
        {
            PrintMenuOption($"{i + 1}", _availableLanguages[i]);
        }
        Console.WriteLine();
    }
    
    // Métodos auxiliares para imprimir texto con colores
    private static void PrintColorText(string text, ConsoleColor color)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = originalColor;
    }
    
    private static void PrintConfigItem(string label, string value)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = OptionColor;
        Console.Write($"- {label}: ");
        Console.ForegroundColor = ValueColor;
        Console.WriteLine(value);
        Console.ForegroundColor = originalColor;
    }
    
    private static void PrintMenuOption(string number, string text)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = HighlightColor;
        Console.Write($"{number}. ");
        Console.ForegroundColor = OptionColor;
        Console.WriteLine(text);
        Console.ForegroundColor = originalColor;
    }
}