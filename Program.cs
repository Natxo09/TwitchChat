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

                    string chatMessage = message.Split("PRIVMSG")[1].Split(':', 2)[1];
                    string badges = message.Contains("badges=") 
                        ? _badgeService.GetBadges(message.Split("badges=")[1].Split(";")[0])
                        : "";

                    Console.Write($"[{DateTime.Now:HH:mm:ss}] ");

                    if (!string.IsNullOrEmpty(badges))
                    {
                        Console.Write(badges);
                    }

                    var originalColor = Console.ForegroundColor;
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