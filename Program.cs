// See https://aka.ms/new-console-template for more information

using System.Net.Sockets;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

class Program
{
    static readonly string IconsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons");
    
    static string GetIconOrFallback(string iconName, string fallbackEmoji)
    {
        try
        {
            string iconPath = Path.Combine(IconsPath, $"{iconName}.png");
            if (File.Exists(iconPath))
            {
                byte[] imageBytes = File.ReadAllBytes(iconPath);
                string base64Image = Convert.ToBase64String(imageBytes);
                
                // Intentar protocolo Kitty primero (para Ghostty)
                string kittyProtocol = $"\x1b_Ga=T,f=100,s={imageBytes.Length};{base64Image}\x1b\\";
                
                // Fallback a iTerm2 si Kitty no funciona
                string iterm2Protocol = $"\u001B]1337;File=inline=1:{base64Image}\u0007";
                
                return kittyProtocol + iterm2Protocol;
            }
        }
        catch
        {
            // Si algo falla, usar el emoji
        }
        return fallbackEmoji;
    }

    static ConsoleColor GetUserColor(string username)
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
        string channel = "ohnepixel".ToLower();
        
        using TcpClient client = new TcpClient();
        await client.ConnectAsync("irc.chat.twitch.tv", 6667);
        
        using StreamReader reader = new StreamReader(client.GetStream());
        using StreamWriter writer = new StreamWriter(client.GetStream()) {AutoFlush = true};

        await writer.WriteLineAsync("CAP REQ :twitch.tv/tags twitch.tv/commands twitch.tv/membership");
        await writer.WriteLineAsync("NICK justinfan12345");
        await writer.WriteLineAsync("USER justinfan12345 8 * :justinfan12345");
        await writer.WriteLineAsync($"JOIN #{channel}");
        
        Console.WriteLine($"Conectado al canal: {channel}");
        Console.WriteLine("Mostrando chat (CTRL+C para salir...)");

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
                try
                {
                    string msgId = message.Split("msg-id=")[1].Split(";")[0];
                    string username = message.Split("display-name=")[1].Split(";")[0];
                    string systemMsg = message.Split("system-msg=")[1].Split(";")[0].Replace("\\s", " ");
                    
                    if (msgId.Contains("sub") || msgId.Contains("resub"))
                    {
                        var originalColor = Console.ForegroundColor;
                        Console.WriteLine();
                        Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"★ \u001B[1;3m{systemMsg}\u001B[0m");
                        Console.WriteLine();
                        Console.ForegroundColor = originalColor;
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (message.Contains("PRIVMSG"))
            {
                try
                {
                    string username;
                    string chatMessage;
                    var originalColor = Console.ForegroundColor;

                    if (message.Contains("bits="))
                    {
                        string bits = message.Split("bits=")[1].Split(";")[0];
                        username = message.Split("display-name=")[1].Split(";")[0];
                        string cheerMessage = message.Split("PRIVMSG")[1].Split(':', 2)[1];
                        
                        cheerMessage = Regex.Replace(cheerMessage, @"[Cc]heer\d+\s*", "");
                        
                        originalColor = Console.ForegroundColor;
                        Console.WriteLine();
                        Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"✦ \u001B[1;3m{username} cheered {bits} bits: {cheerMessage}\u001B[0m");
                        Console.WriteLine();
                        Console.ForegroundColor = originalColor;
                        continue;
                    }
                    
                    string badges = "";
                    if (message.Contains("badges="))
                    {
                        var badgePart = message.Split("badges=")[1].Split(";")[0];
                        if (badgePart.Contains("moderator")) badges += GetIconOrFallback("moderator", "🛡️ ");
                        if (badgePart.Contains("sub")) badges += GetIconOrFallback("sub", "⭐ ");
                        if (badgePart.Contains("vip")) badges += GetIconOrFallback("vip", "💎 ");
                    }

                    if (message.Contains("display-name="))
                    {
                        username = message.Split("display-name=")[1].Split(";")[0];
                        chatMessage = message.Split("PRIVMSG")[1].Split(':', 2)[1];
                    }
                    else
                    {
                        int exclamationIndex = message.IndexOf("!");
                        username = message.Substring(1, exclamationIndex - 1);
                        chatMessage = message.Split("PRIVMSG")[1].Split(':', 2)[1];
                    }

                    if (string.IsNullOrEmpty(username)) continue;

                    Console.Write($"[{DateTime.Now:HH:mm:ss}] ");

                    if (!string.IsNullOrEmpty(badges))
                    {
                        Console.Write(badges);
                    }

                    Console.ForegroundColor = GetUserColor(username);
                    Console.Write($"{username}");

                    Console.ForegroundColor = originalColor;
                    Console.WriteLine($": {chatMessage}");
                }
                catch
                {
                    continue;
                }
            }
        }
    }
}