using System;
using System.Text.RegularExpressions;

public class BitsMessageHandler
{
    public bool HandleMessage(string message, string username)
    {
        if (!message.Contains("bits=")) return false;

        try
        {
            string bits = message.Split("bits=")[1].Split(";")[0];
            string cheerMessage = message.Split("PRIVMSG")[1].Split(':', 2)[1];
            
            cheerMessage = Regex.Replace(cheerMessage, @"[Cc]heer\d+\s*", "");
            
            var originalColor = Console.ForegroundColor;
            Console.WriteLine();
            Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"✦ \u001B[1;3m{username} cheered {bits} bits: {cheerMessage}\u001B[0m");
            Console.WriteLine();
            Console.ForegroundColor = originalColor;
            return true;
        }
        catch
        {
            return false;
        }
    }
} 