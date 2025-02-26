using System;
using System.Threading.Tasks;

public class TranslatedMessageHandler
{
    private readonly TranslationService _translationService;
    private readonly BadgeService _badgeService;

    public TranslatedMessageHandler(TranslationService translationService, BadgeService badgeService)
    {
        _translationService = translationService;
        _badgeService = badgeService;
    }

    public async Task HandleMessageAsync(string message, string username)
    {
        try
        {
            string chatMessage = message.Split("PRIVMSG")[1].Split(':', 2)[1];
            string badges = message.Contains("badges=") 
                ? _badgeService.GetBadges(message.Split("badges=")[1].Split(";")[0])
                : "";

            // Display original message
            Console.Write($"[{DateTime.Now:HH:mm:ss}] ");

            if (!string.IsNullOrEmpty(badges))
            {
                Console.Write(badges);
            }

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = Program.GetUserColor(username);
            Console.Write($"{username}");
            Console.ForegroundColor = originalColor;
            Console.WriteLine($": {chatMessage}");

            // Display translated message if translation is enabled
            if (_translationService.IsEnabled)
            {
                string translatedMessage = await _translationService.TranslateMessageAsync(chatMessage);
                
                if (translatedMessage != chatMessage)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"    ↳ {translatedMessage}");
                    Console.ForegroundColor = originalColor;
                }
            }
        }
        catch
        {
            // Ignore malformed messages
        }
    }

    private string GetFlagEmoji(string language)
    {
        return language.ToLower() switch
        {
            "english" => "🇬🇧",
            "spanish" => "🇪🇸",
            "french" => "🇫🇷",
            "german" => "🇩🇪",
            "italian" => "🇮🇹",
            "portuguese" => "🇵🇹",
            "japanese" => "🇯🇵",
            "korean" => "🇰🇷",
            "chinese" => "🇨🇳",
            "russian" => "🇷🇺",
            _ => "🌐"
        };
    }
} 