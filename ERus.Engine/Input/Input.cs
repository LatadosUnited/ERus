using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERus.Engine.Input;

public static class Input
{
    private static InputProfile? _activeProfile;

    // We keep a reference to be accessed by InputModule
    internal static InputProfile? ActiveProfile => _activeProfile;

    public static void LoadProfile(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    Converters = { new JsonStringEnumConverter() },
                    PropertyNameCaseInsensitive = true 
                };
                string json = File.ReadAllText(filePath);
                _activeProfile = JsonSerializer.Deserialize<InputProfile>(json, options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Input] Falha ao carregar profile {filePath}: {ex.Message}");
            }
        }
        
        if (_activeProfile == null)
            _activeProfile = new InputProfile();
    }

    /// <summary>
    /// Busca uma ação no mapa especificado. Deve ser chamado preferencialmente no OnInit() do seu script e feito cache.
    /// </summary>
    public static InputAction? GetAction(string mapName, string actionName)
    {
        if (_activeProfile == null) return null;

        for (int i = 0; i < _activeProfile.Maps.Count; i++)
        {
            if (_activeProfile.Maps[i].Name == mapName)
            {
                var map = _activeProfile.Maps[i];
                for (int j = 0; j < map.Actions.Count; j++)
                {
                    if (map.Actions[j].Name == actionName)
                    {
                        return map.Actions[j];
                    }
                }
            }
        }
        return null;
    }
}
