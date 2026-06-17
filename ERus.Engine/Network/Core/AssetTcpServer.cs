using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using System.Text;

namespace ERus.Engine.Network.Core;

public class AssetTcpServer
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly AssetSyncManager _assetSyncManager;
    private Task? _serverTask;

    public AssetTcpServer(AssetSyncManager assetSyncManager)
    {
        _assetSyncManager = assetSyncManager;
    }

    public void Start(int port)
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Console.WriteLine($"[AssetTcpServer] Escutando na porta TCP {port}");

        _serverTask = Task.Run(() => AcceptClientsAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
    }

    private async Task AcceptClientsAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var client = await _listener!.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleClientAsync(client, token));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[AssetTcpServer] Erro no Accept: {ex.Message}");
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            try
            {
                // Protocol: Client sends hash length (4 bytes), then hash string (UTF8)
                byte[] lengthBuffer = new byte[4];
                int bytesRead = await stream.ReadAsync(lengthBuffer, 0, 4, token);
                if (bytesRead != 4) return;

                int hashLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (hashLength <= 0 || hashLength > 256) return; // limit hash size

                byte[] hashBuffer = new byte[hashLength];
                bytesRead = await stream.ReadAsync(hashBuffer, 0, hashLength, token);
                if (bytesRead != hashLength) return;

                string hash = Encoding.UTF8.GetString(hashBuffer);
                Console.WriteLine($"[AssetTcpServer] Cliente solicitou Hash: {hash}");

                string? filePath = _assetSyncManager.GetFilePathByHash(hash);
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    // Respond with 0 file size (Not found)
                    await stream.WriteAsync(BitConverter.GetBytes(0L), 0, 8, token);
                    return;
                }

                FileInfo fi = new FileInfo(filePath);
                long fileSize = fi.Length;
                
                // Respond with file size
                await stream.WriteAsync(BitConverter.GetBytes(fileSize), 0, 8, token);

                // Transfer file
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(8192); // 8KB chunks
                    try
                    {
                        int read;
                        while ((read = await fs.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                        {
                            await stream.WriteAsync(buffer, 0, read, token);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                Console.WriteLine($"[AssetTcpServer] Transferência de {hash} concluída com sucesso.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetTcpServer] Erro com cliente: {ex.Message}");
            }
        }
    }
}
