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
            
            // Solo mostrar si hay una traducción diferente
            if (!string.IsNullOrEmpty(translatedMessage) && translatedMessage != message)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("    ↳ ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"{translatedMessage}");
                Console.ForegroundColor = originalColor;
            }
        }
        catch
        {
            // Ignorar errores de traducción en producción
        }
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
            Console.WriteLine($"    ↳ [ERROR] {message}");
            Console.ForegroundColor = originalColor;
        }
    }
} 