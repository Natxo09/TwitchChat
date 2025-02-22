public class SubMessageHandler
{
    public void HandleMessage(string message)
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
            // Ignorar mensajes mal formateados
        }
    }
} 