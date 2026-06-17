using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using LiteNetLib;

namespace ERus.Engine.Network.Core;

public class AssetCacheManifest
{
    public Dictionary<string, AssetCacheEntry> Entries { get; set; } = new Dictionary<string, AssetCacheEntry>();
}

public class AssetCacheEntry
{
    public string FilePath { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}

public class AssetSyncManager
{
    private readonly NetworkManager _networkManager;
    private AssetTcpServer? _tcpServer;
    private AssetTcpClient? _tcpClient;
    
    private readonly string _cacheDirectory;
    private readonly string _manifestPath;
    
    private AssetCacheManifest _manifest = new AssetCacheManifest();
    private ConcurrentDictionary<string, string> _hashToFileMap = new ConcurrentDictionary<string, string>();
    private ConcurrentDictionary<string, bool> _activeDownloads = new ConcurrentDictionary<string, bool>();
    private SemaphoreSlim _downloadSemaphore = new SemaphoreSlim(4);

    public AssetSyncManager(NetworkManager networkManager)
    {
        _networkManager = networkManager;
        
        _cacheDirectory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Assets"));
        if (!Directory.Exists(_cacheDirectory))
            Directory.CreateDirectory(_cacheDirectory);
            
        _manifestPath = Path.Combine(_cacheDirectory, ".erus_sync_cache.json");
        LoadManifest();
    }

    private void LoadManifest()
    {
        try
        {
            if (File.Exists(_manifestPath))
            {
                string json = File.ReadAllText(_manifestPath);
                var manifest = JsonSerializer.Deserialize<AssetCacheManifest>(json);
                if (manifest != null)
                {
                    _manifest = manifest;
                    foreach (var kvp in _manifest.Entries)
                    {
                        // Check if file still exists and timestamp is matching
                        if (File.Exists(kvp.Key))
                        {
                            var fi = new FileInfo(kvp.Key);
                            if (fi.LastWriteTimeUtc == kvp.Value.LastModified)
                            {
                                _hashToFileMap[kvp.Value.Hash] = kvp.Key;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AssetSyncManager] Erro ao carregar manifesto: {ex.Message}");
        }
    }

    private void SaveManifest()
    {
        try
        {
            string json = JsonSerializer.Serialize(_manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_manifestPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AssetSyncManager] Erro ao salvar manifesto: {ex.Message}");
        }
    }

    public string? GetFilePathByHash(string hash)
    {
        if (_hashToFileMap.TryGetValue(hash, out var path))
            return path;
        return null;
    }

    public string CalculateAndRegisterAsset(string filePath)
    {
        var fi = new FileInfo(filePath);
        if (_manifest.Entries.TryGetValue(filePath, out var entry))
        {
            if (fi.LastWriteTimeUtc == entry.LastModified)
            {
                return entry.Hash; // Already cached
            }
        }

        // Needs hash calculation
        using (var md5 = SHA256.Create())
        using (var stream = File.OpenRead(filePath))
        {
            byte[] hashBytes = md5.ComputeHash(stream);
            string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            var newEntry = new AssetCacheEntry
            {
                FilePath = filePath,
                Hash = hash,
                LastModified = fi.LastWriteTimeUtc
            };

            _manifest.Entries[filePath] = newEntry;
            _hashToFileMap[hash] = filePath;
            SaveManifest();

            return hash;
        }
    }

    public void RegisterDownloadedAsset(string hash, string filePath)
    {
        var fi = new FileInfo(filePath);
        var newEntry = new AssetCacheEntry
        {
            FilePath = filePath,
            Hash = hash,
            LastModified = fi.LastWriteTimeUtc
        };

        _manifest.Entries[filePath] = newEntry;
        _hashToFileMap[hash] = filePath;
        SaveManifest();
        
        _activeDownloads.TryRemove(hash, out _);
    }

    public void StartServer(int tcpPort)
    {
        _tcpServer = new AssetTcpServer(this);
        _tcpServer.Start(tcpPort);
    }

    public void StopServer()
    {
        _tcpServer?.Stop();
    }

    public void SetupClient(string serverIp, int tcpPort)
    {
        _tcpClient = new AssetTcpClient(this, serverIp, tcpPort);
    }

    // Chamado pelo NetworkManager quando o botão "Play" ou usuário arrasta asset
    public void AnnounceAsset(string filePath)
    {
        string hash = CalculateAndRegisterAsset(filePath);
        var fi = new FileInfo(filePath);

        var packet = new AssetAnnouncePacket
        {
            Hash = hash,
            FileName = Path.GetFileName(filePath),
            FileSize = fi.Length
        };

        _networkManager.SendAssetAnnounce(packet);
        Console.WriteLine($"[AssetSync] Anunciando asset {packet.FileName} (Hash: {hash})");
    }

    // Chamado pelo Dispatcher quando recebe pacote AssetAnnouncePacket
    public void OnAssetAnnouncedReceived(AssetAnnouncePacket packet)
    {
        Console.WriteLine($"[AssetSync] Recebeu anúncio de asset: {packet.FileName} ({packet.Hash})");
        
        if (!_hashToFileMap.ContainsKey(packet.Hash))
        {
            if (_activeDownloads.TryAdd(packet.Hash, true))
            {
                Console.WriteLine($"[AssetSync] Asset não encontrado localmente. Iniciando download TCP...");
                string targetPath = Path.Combine(_cacheDirectory, packet.FileName);
                
                // Evita sobrescrever se arquivo com mesmo nome existir, cria nome único.
                if (File.Exists(targetPath))
                {
                    targetPath = Path.Combine(_cacheDirectory, $"{Guid.NewGuid().ToString().Substring(0,8)}_{packet.FileName}");
                }

                _ = Task.Run(async () => 
                {
                    await _downloadSemaphore.WaitAsync();
                    try
                    {
                        if (_tcpClient != null)
                        {
                            bool success = await _tcpClient.DownloadAssetAsync(packet.Hash, targetPath, CancellationToken.None);
                            if (!success)
                            {
                                _activeDownloads.TryRemove(packet.Hash, out _);
                            }
                        }
                    }
                    finally
                    {
                        _downloadSemaphore.Release();
                    }
                });
            }
        }
    }
}
