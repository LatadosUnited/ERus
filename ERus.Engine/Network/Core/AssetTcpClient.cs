using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using System.Security.Cryptography;

namespace ERus.Engine.Network.Core;

public class AssetTcpClient
{
    private readonly AssetSyncManager _assetSyncManager;
    private readonly string _serverIp;
    private readonly int _serverPort;

    public AssetTcpClient(AssetSyncManager assetSyncManager, string serverIp, int serverPort)
    {
        _assetSyncManager = assetSyncManager;
        _serverIp = serverIp;
        _serverPort = serverPort;
    }

    public async Task<bool> DownloadAssetAsync(string hash, string targetFilePath, CancellationToken token)
    {
        try
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(_serverIp, _serverPort, token);
                using (var stream = client.GetStream())
                {
                    // Protocol: Send hash length (4 bytes), then hash string (UTF8)
                    byte[] hashBytes = Encoding.UTF8.GetBytes(hash);
                    byte[] lengthBuffer = BitConverter.GetBytes(hashBytes.Length);
                    
                    await stream.WriteAsync(lengthBuffer, 0, 4, token);
                    await stream.WriteAsync(hashBytes, 0, hashBytes.Length, token);

                    // Read file size (8 bytes)
                    byte[] sizeBuffer = new byte[8];
                    int bytesRead = await stream.ReadAsync(sizeBuffer, 0, 8, token);
                    if (bytesRead != 8) return false;

                    long expectedFileSize = BitConverter.ToInt64(sizeBuffer, 0);
                    if (expectedFileSize <= 0)
                    {
                        Console.WriteLine($"[AssetTcpClient] Servidor não encontrou o arquivo do Hash {hash}.");
                        return false;
                    }

                    string tempFilePath = targetFilePath + ".part";
                    long totalRead = 0;

                    using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192); // 8KB chunks
                        try
                        {
                            int read;
                            while (totalRead < expectedFileSize && (read = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                            {
                                await fs.WriteAsync(buffer, 0, read, token);
                                totalRead += read;
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }

                    if (totalRead == expectedFileSize)
                    {
                        string computedHash = "";
                        using (var md5 = SHA256.Create())
                        using (var streamHash = File.OpenRead(tempFilePath))
                        {
                            byte[] computedHashBytes = md5.ComputeHash(streamHash);
                            computedHash = BitConverter.ToString(computedHashBytes).Replace("-", "").ToLowerInvariant();
                        }

                        if (computedHash != hash.ToLowerInvariant())
                        {
                            Console.WriteLine($"[AssetTcpClient] Download de {hash} falhou na validação de integridade. (Recebido: {computedHash})");
                            if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                            return false;
                        }

                        // Se concluiu com sucesso e validou, renomeia o .part para final
                        if (File.Exists(targetFilePath)) File.Delete(targetFilePath);
                        File.Move(tempFilePath, targetFilePath);
                        Console.WriteLine($"[AssetTcpClient] Download de {hash} concluído com sucesso ({totalRead} bytes).");
                        
                        _assetSyncManager.RegisterDownloadedAsset(hash, targetFilePath);
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"[AssetTcpClient] Download de {hash} corrompido ou incompleto.");
                        if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                        return false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AssetTcpClient] Falha ao baixar asset {hash}: {ex.Message}");
            return false;
        }
    }
}
