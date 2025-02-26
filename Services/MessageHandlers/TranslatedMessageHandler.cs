using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

public class TranslatedMessageHandler
{
    private readonly TranslationService _translationService;
    private readonly BadgeService _badgeService;
    // Regular expression to detect URLs in chat messages
    private static readonly Regex UrlRegex = new Regex(@"(https?:\/\/[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
            
            // Instead of directly writing the message, process it to highlight URLs
            Console.Write(": ");
            WriteMessageWithHighlightedLinks(chatMessage, originalColor);
            Console.WriteLine();

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
    
    // Helper method to write a message with highlighted links
    private void WriteMessageWithHighlightedLinks(string message, ConsoleColor originalColor)
    {
        int lastIndex = 0;
        foreach (Match match in UrlRegex.Matches(message))
        {
            // Write the text before the URL
            Console.Write(message.Substring(lastIndex, match.Index - lastIndex));
            
            // Write the URL in purple and italic
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write($"\u001B[3m{match.Value}\u001B[0m"); // \u001B[3m for italic, \u001B[0m to reset
            Console.ForegroundColor = originalColor;
            
            lastIndex = match.Index + match.Length;
        }
        
        // Write any remaining text after the last URL
        if (lastIndex < message.Length)
        {
            Console.Write(message.Substring(lastIndex));
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
                
                // Also highlight links in the translated message
                WriteMessageWithHighlightedLinks(translatedMessage, ConsoleColor.White);
                Console.WriteLine();
                
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