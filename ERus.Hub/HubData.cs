using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ERus.Hub;

public class ProjectData
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}

public class EngineInstall
{
    public string VersionName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
}

public class HubConfig
{
    public List<ProjectData> Projects { get; set; } = new List<ProjectData>();
    public List<EngineInstall> Installs { get; set; } = new List<EngineInstall>();
}

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
