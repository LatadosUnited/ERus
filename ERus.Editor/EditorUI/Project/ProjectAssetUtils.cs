using System;
using System.IO;
using System.Numerics;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ERus.Engine.Scripting;

namespace ERus.Editor.EditorUI.Project;

public static class ProjectAssetUtils
{
    public static bool IsImageFile(string filePath)
    {
        return filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".tga", StringComparison.OrdinalIgnoreCase);
    }

    public static (string Icon, Vector4 Color) GetFileIconAndColor(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" => (FontAwesome.Code, new Vector4(0.4f, 0.85f, 0.4f, 1.0f)),
            ".scene" => (FontAwesome.Camera, new Vector4(0.9f, 0.45f, 0.3f, 1.0f)),
            ".prefab" => (FontAwesome.Cube, new Vector4(0.3f, 0.75f, 1.0f, 1.0f)),
            ".obj" or ".fbx" or ".gltf" or ".glb" => (FontAwesome.CubeSolid, new Vector4(0.85f, 0.65f, 1.0f, 1.0f)),
            ".json" => (FontAwesome.File, new Vector4(0.95f, 0.8f, 0.3f, 1.0f)),
            ".wav" or ".mp3" or ".ogg" => (FontAwesome.Play, new Vector4(1.0f, 0.5f, 0.7f, 1.0f)),
            _ => (FontAwesome.File, new Vector4(0.7f, 0.7f, 0.7f, 1.0f))
        };
    }

    public static string TruncateString(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength - 2) + "..";
    }

    public static void OpenFileExternally(string filePath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", $"\"{filePath}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"\"{filePath}\"");
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Project] Erro ao abrir arquivo externamente: {ex.Message}");
        }
    }

    public static void ShowInExplorer(string filePath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Project] Erro ao abrir no Explorer: {ex.Message}");
        }
    }
}
