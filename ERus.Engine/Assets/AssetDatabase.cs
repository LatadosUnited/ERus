using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace ERus.Engine.Assets;

public class AssetMeta
{
    public Guid Guid { get; set; }
    public string Hash { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}

public class AssetDatabase
{
    private readonly string _assetsDirectory;
    private readonly Dictionary<Guid, string> _guidToPath = new();
    private readonly Dictionary<string, Guid> _hashToGuid = new();
    
    public AssetDatabase(string assetsDirectory)
    {
        _assetsDirectory = assetsDirectory;
        if (!Directory.Exists(_assetsDirectory))
        {
            Directory.CreateDirectory(_assetsDirectory);
        }
    }

    public void Scan()
    {
        _guidToPath.Clear();
        _hashToGuid.Clear();
        
        if (!Directory.Exists(_assetsDirectory)) return;

        var files = Directory.GetFiles(_assetsDirectory, "*.*", SearchOption.AllDirectories);
        
        foreach (var file in files)
        {
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

            ProcessFile(file);
        }
    }

    public void ProcessFile(string filePath)
    {
        string metaPath = filePath + ".meta";
        var fi = new FileInfo(filePath);
        AssetMeta meta;

        bool needsSave = false;

        if (File.Exists(metaPath))
        {
            try
            {
                string json = File.ReadAllText(metaPath);
                meta = JsonSerializer.Deserialize<AssetMeta>(json) ?? new AssetMeta { Guid = Guid.NewGuid() };
            }
            catch
            {
                meta = new AssetMeta { Guid = Guid.NewGuid() };
                needsSave = true;
            }

            // Checa se o hash está atualizado
            if (meta.LastModified != fi.LastWriteTimeUtc)
            {
                meta.Hash = CalculateHash(filePath);
                meta.LastModified = fi.LastWriteTimeUtc;
                needsSave = true;
            }
        }
        else
        {
            meta = new AssetMeta
            {
                Guid = Guid.NewGuid(),
                Hash = CalculateHash(filePath),
                LastModified = fi.LastWriteTimeUtc
            };
            needsSave = true;
        }

        if (needsSave)
        {
            SaveMeta(metaPath, meta);
        }

        _guidToPath[meta.Guid] = filePath;
        if (!string.IsNullOrEmpty(meta.Hash))
        {
            _hashToGuid[meta.Hash] = meta.Guid;
        }
    }

    private string CalculateHash(string filePath)
    {
        using (var md5 = SHA256.Create())
        using (var stream = File.OpenRead(filePath))
        {
            byte[] hashBytes = md5.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }

    private void SaveMeta(string metaPath, AssetMeta meta)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(meta, options);
            File.WriteAllText(metaPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AssetDatabase] Erro ao salvar meta {metaPath}: {ex.Message}");
        }
    }

    public string? GetPathByGuid(Guid guid)
    {
        if (_guidToPath.TryGetValue(guid, out string? path))
        {
            if (File.Exists(path)) return path;
            
            // Fallback: se não existe, talvez o usuário tenha deletado fora da engine.
            // Poderíamos invocar um Scan aqui, mas melhor retornar null
            return null;
        }
        return null;
    }

    public Guid? GetGuidByPath(string path)
    {
        string metaPath = path + ".meta";
        if (File.Exists(metaPath))
        {
            try
            {
                string json = File.ReadAllText(metaPath);
                var meta = JsonSerializer.Deserialize<AssetMeta>(json);
                if (meta != null) return meta.Guid;
            }
            catch { }
        }
        return null;
    }

    public string? GetPathByHash(string hash)
    {
        if (_hashToGuid.TryGetValue(hash, out Guid guid))
        {
            return GetPathByGuid(guid);
        }
        return null;
    }
}
