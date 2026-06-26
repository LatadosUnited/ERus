using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using LiteNetLib;
using ERus.Engine.Network.Packets.Assets;

namespace ERus.Engine.Network.Core;



public class AssetSyncManager
{
    private readonly NetworkManager _networkManager;
    private AssetTcpServer? _tcpServer;
    private AssetTcpClient? _tcpClient;
    

    private ConcurrentDictionary<string, bool> _activeDownloads = new ConcurrentDictionary<string, bool>();
    private SemaphoreSlim _downloadSemaphore = new SemaphoreSlim(4);

    public event Action<string, string>? OnAssetDownloaded;

    public AssetSyncManager(NetworkManager networkManager)
    {
        _networkManager = networkManager;
        
        string cacheDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Assets"));
        if (!Directory.Exists(cacheDir))
            Directory.CreateDirectory(cacheDir);
    }

    public string? GetFilePathByHash(string hash)
    {
        return ERus.Engine.Core.Engine.Instance?.AssetDatabase.GetPathByHash(hash);
    }

    public void RegisterDownloadedAsset(string hash, string filePath)
    {
        ERus.Engine.Core.Engine.Instance?.AssetDatabase.ProcessFile(filePath);
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

    public async Task AnnounceAssetAsync(string filePath, Action<string>? onHashReady = null)
    {
        ERus.Engine.Core.Engine.Instance?.AssetDatabase.ProcessFile(filePath);
        var guid = ERus.Engine.Core.Engine.Instance?.AssetDatabase.GetGuidByPath(filePath);
        
        string hash = "";
        if (guid.HasValue)
        {
            // O AssetDatabase gera o Hash no .meta internamente. 
            // Porém, como não salvamos o Hash explicitamente em memória num dicionário reverso fácil de ler aqui,
            // podemos ler o .meta que acabou de ser salvo ou pegar pelo AssetDatabase.
            // Para ser prático e assíncrono caso seja gigante, vamos ler do .meta.
            string metaPath = filePath + ".meta";
            if (File.Exists(metaPath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(metaPath);
                    var meta = JsonSerializer.Deserialize<ERus.Engine.Assets.AssetMeta>(json);
                    if (meta != null) hash = meta.Hash;
                }
                catch { }
            }
        }
        
        if (string.IsNullOrEmpty(hash)) return;

        var fi = new FileInfo(filePath);

        var packet = new AssetAnnouncePacket
        {
            Hash = hash,
            FileName = Path.GetFileName(filePath),
            FileSize = fi.Length
        };

        _networkManager.SendAssetAnnounce(packet);
        Console.WriteLine($"[AssetSync] Anunciando asset {packet.FileName} (Hash: {hash}) para todos");
        
        onHashReady?.Invoke(hash);
    }

    public async Task AnnounceAssetToPeerAsync(string filePath, NetPeer peer)
    {
        ERus.Engine.Core.Engine.Instance?.AssetDatabase.ProcessFile(filePath);
        var guid = ERus.Engine.Core.Engine.Instance?.AssetDatabase.GetGuidByPath(filePath);
        
        string hash = "";
        if (guid.HasValue)
        {
            string metaPath = filePath + ".meta";
            if (File.Exists(metaPath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(metaPath);
                    var meta = JsonSerializer.Deserialize<ERus.Engine.Assets.AssetMeta>(json);
                    if (meta != null) hash = meta.Hash;
                }
                catch { }
            }
        }
        
        if (string.IsNullOrEmpty(hash)) return;

        var fi = new FileInfo(filePath);

        var packet = new AssetAnnouncePacket
        {
            Hash = hash,
            FileName = Path.GetFileName(filePath),
            FileSize = fi.Length
        };

        _networkManager.Dispatcher.SendToPeer(peer, packet, DeliveryMethod.ReliableOrdered);
        Console.WriteLine($"[AssetSync] Anunciando asset {packet.FileName} (Hash: {hash}) para peer {peer.Id}");
    }

    // Chamado pelo Dispatcher quando recebe pacote AssetAnnouncePacket
    public void OnAssetAnnouncedReceived(AssetAnnouncePacket packet)
    {
        Console.WriteLine($"[AssetSync] Recebeu anúncio de asset: {packet.FileName} ({packet.Hash})");
        string? localPath = ERus.Engine.Core.Engine.Instance?.AssetDatabase.GetPathByHash(packet.Hash);
        if (string.IsNullOrEmpty(localPath))
        {
            if (_activeDownloads.TryAdd(packet.Hash, true))
            {
                Console.WriteLine($"[AssetSync] Solicitando download do asset: {packet.FileName}");
                string cacheDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Assets", "Downloads"));
                if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

                string targetPath = Path.Combine(cacheDir, packet.FileName);
                // Evitar colisão de nome para arquivos diferentes
                if (File.Exists(targetPath))
                {
                    targetPath = Path.Combine(cacheDir, $"{Guid.NewGuid().ToString().Substring(0,8)}_{packet.FileName}");
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
                            else
                            {
                                RegisterDownloadedAsset(packet.Hash, targetPath);
                                OnAssetDownloaded?.Invoke(packet.Hash, targetPath);
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
