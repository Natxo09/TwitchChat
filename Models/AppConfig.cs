using System;
using System.IO;
using System.Text.Json;

public class AppConfig
{
    private static readonly string ConfigFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.json");
    
    public string TwitchChannel { get; set; } = "orslok";
    public string TargetLanguage { get; set; } = "English";
    public bool TranslationEnabled { get; set; } = true;
    
    // Nuevas opciones para controlar la velocidad
    public int MaxMessageLength { get; set; } = 200;
    public int CacheSize { get; set; } = 100;
    public int TimeoutSeconds { get; set; } = 5;
    
    // Opción para activar/desactivar el modo de depuración
    public bool DebugMode { get; set; } = false;
    
    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                return config ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading config: {ex.Message}");
        }
        
        return new AppConfig();
    }
    
    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving config: {ex.Message}");
        }
    }
} 