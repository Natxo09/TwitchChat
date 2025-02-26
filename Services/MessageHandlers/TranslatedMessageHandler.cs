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

    public void HandleMessage(string message, string username)
    {
        try
        {
            string chatMessage = message.Split("PRIVMSG")[1].Split(':', 2)[1];
            string badges = message.Contains("badges=") 
                ? _badgeService.GetBadges(message.Split("badges=")[1].Split(";")[0])
                : "";

            // Display original message immediately
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

            // Start translation in background if enabled
            if (_translationService.IsEnabled)
            {
                // No await here - let translation happen in background
                _ = TranslateAndDisplayAsync(chatMessage, originalColor);
            }
        }
        catch
        {
            // Ignore malformed messages
        }
    }
    
    private async Task TranslateAndDisplayAsync(string message, ConsoleColor originalColor)
    {
        try
        {
            string translatedMessage = await _translationService.TranslateMessageAsync(message);
            
            // Solo mostrar si la traducción es diferente del mensaje original
            if (translatedMessage != message)
            {
                lock (Console.Out) // Evitar que otros hilos escriban en la consola al mismo tiempo
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"    ↳ {translatedMessage}");
                    Console.ForegroundColor = originalColor;
                }
            }
        }
        catch
        {
            // Ignorar errores en la traducción en segundo plano
        }
    }
} 