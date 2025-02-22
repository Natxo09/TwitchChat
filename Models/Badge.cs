using System;
using System.IO;
using System.Text;

public class Badge
{
    private static readonly string IconsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons");
    
    public string Type { get; }
    public string IconName { get; }
    public string FallbackEmoji { get; }

    public Badge(string type, string iconName, string fallbackEmoji)
    {
        Type = type;
        IconName = iconName;
        FallbackEmoji = fallbackEmoji;
    }

    public string GetIcon()
    {
        try
        {
            string iconPath = Path.Combine(IconsPath, $"{IconName}.png");
            if (File.Exists(iconPath))
            {
                byte[] imageBytes = File.ReadAllBytes(iconPath);
                string base64Image = Convert.ToBase64String(imageBytes);
                
                string kittyProtocol = $"\x1b_Ga=T,f=100,s={imageBytes.Length};{base64Image}\x1b\\";
                string iterm2Protocol = $"\u001B]1337;File=inline=1:{base64Image}\u0007";
                
                return kittyProtocol + iterm2Protocol;
            }
        }
        catch
        {
            // Si algo falla, usar el emoji
        }
        return FallbackEmoji;
    }
} 