using System;
using System.IO;
using System.Text.Json;

namespace ERus.Hub;

public static class ConfigManager
{
    private static string GetConfigPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string directory = Path.Combine(appData, "ERusHub");
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        return Path.Combine(directory, "config.json");
    }

    public static HubConfig Load()
    {
        string path = GetConfigPath();
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<HubConfig>(json);
                if (config != null)
                    return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao ler o arquivo de config: {ex.Message}");
            }
        }
        return new HubConfig();
    }

    public static void Save(HubConfig config)
    {
        try
        {
            string path = GetConfigPath();
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao salvar o arquivo de config: {ex.Message}");
        }
    }
}
