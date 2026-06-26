using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERus.Engine.Input;

public static class Input
{
    private static InputProfile? _activeProfile;

    // We keep a reference to be accessed by InputModule and Editor
    public static InputProfile? ActiveProfile => _activeProfile;

    /// <summary>
    /// Posição atual do mouse em coordenadas da tela (ou GameView).
    /// </summary>
    public static System.Numerics.Vector2 MousePosition { get; set; } = System.Numerics.Vector2.Zero;

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

    public static void SaveProfile(string filePath)
    {
        if (_activeProfile == null) return;

        try
        {
            var options = new JsonSerializerOptions 
            { 
                Converters = { new JsonStringEnumConverter() },
                WriteIndented = true
            };
            string json = JsonSerializer.Serialize(_activeProfile, options);
            File.WriteAllText(filePath, json);
            Console.WriteLine($"[Input] Profile salvo em {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Input] Falha ao salvar profile {filePath}: {ex.Message}");
        }
    }
}
