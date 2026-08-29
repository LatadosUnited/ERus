using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;

namespace ERus.Engine.Network.Core;

public class NetworkTransport : INetEventListener
{
    public const string DefaultSessionToken = "ERusDefaultSession";

    private NetManager? _netManager;
    private readonly ConcurrentDictionary<int, int> _peerIdToUserId = new();

    public bool IsHost { get; private set; }
    public bool IsClient => _netManager != null && _netManager.IsRunning && !IsHost;
    public bool IsConnected => _netManager != null && _netManager.IsRunning && (IsHost ? true : _netManager.FirstPeer?.ConnectionState == ConnectionState.Connected);
    public int MyUserId { get; private set; }
    public int ConnectedPeersCount => _netManager?.ConnectedPeersCount ?? 0;
    public string SessionToken { get; set; } = DefaultSessionToken;

    // Eventos
    public Action<NetPeer>? OnPeerConnectedEvent;
    public Action<NetPeer, DisconnectInfo>? OnPeerDisconnectedEvent;
    public Action<NetPeer, NetPacketReader, byte, DeliveryMethod>? OnNetworkReceiveEvent;

    public void InitializeAsHost(int port, string sessionToken = "")
    {
        IsHost = true;
        MyUserId = 0; // O Host tem sempre a autoridade máxima (ID 0)
        SessionToken = string.IsNullOrWhiteSpace(sessionToken) ? DefaultSessionToken : sessionToken;
        _netManager = new NetManager(this) { ChannelsCount = 2 };
        _netManager.Start(port);
        Console.WriteLine($"[Network] Host iniciado na porta {port} (Token: {(SessionToken == DefaultSessionToken ? "Padrão" : "Personalizado")})");
    }

    public void InitializeAsServer(int port, string sessionToken = "")
    {
        IsHost = true; // Servidor dedicado é a autoridade (Host)
        MyUserId = 0;
        SessionToken = string.IsNullOrWhiteSpace(sessionToken) ? DefaultSessionToken : sessionToken;
        _netManager = new NetManager(this) { ChannelsCount = 2 };
        _netManager.Start(port);
        Console.WriteLine($"[Network] Servidor Dedicado iniciado na porta {port} (Token: {(SessionToken == DefaultSessionToken ? "Padrão" : "Personalizado")})");
    }

    public void InitializeAsClient(string ip, int port, string sessionToken = "")
    {
        IsHost = false;
        SessionToken = string.IsNullOrWhiteSpace(sessionToken) ? DefaultSessionToken : sessionToken;
        
        // Geramos ID de client estável e positivo baseado em GUID para evitar colisões
        int generatedId = Guid.NewGuid().GetHashCode() & 0x7FFFFFFF;
        MyUserId = generatedId == 0 ? 1 : generatedId;
        _netManager = new NetManager(this) { ChannelsCount = 2 };
        _netManager.Start();
        _netManager.Connect(ip, port, SessionToken);
        Console.WriteLine($"[Network] Client conectando a {ip}:{port} com User ID {MyUserId}...");
    }

    public void RegisterPeerUser(int peerId, int userId)
    {
        _peerIdToUserId[peerId] = userId;
    }

    public int GetUserIdForPeer(int peerId)
    {
        return _peerIdToUserId.TryGetValue(peerId, out int userId) ? userId : peerId;
    }

    public void UnregisterPeer(int peerId)
    {
        _peerIdToUserId.TryRemove(peerId, out _);
    }

    public void PollEvents() => _netManager?.PollEvents();
    
    public void Stop()
    {
        _netManager?.Stop();
        _peerIdToUserId.Clear();
    }

    public NetManager? NetManager => _netManager;

    // --- INetEventListener ---
    public void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine($"[Network] Peer conectado: {peer.Id}");
        OnPeerConnectedEvent?.Invoke(peer);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        Console.WriteLine($"[Network] Peer desconectado: {peer.Id} Motivo: {info.Reason}");
        OnPeerDisconnectedEvent?.Invoke(peer, info);
        UnregisterPeer(peer.Id);
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError error) { }
    
    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod method)
    {
        OnNetworkReceiveEvent?.Invoke(peer, reader, channelNumber, method);
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        if (_netManager != null && _netManager.ConnectedPeersCount < 10)
        {
            var peer = request.AcceptIfKey(SessionToken);
            if (peer == null)
            {
                Console.WriteLine($"[Network] Conexão rejeitada de {request.RemoteEndPoint}: Token de sessão inválido.");
            }
        }
        else
        {
            request.Reject();
            Console.WriteLine($"[Network] Conexão rejeitada de {request.RemoteEndPoint}: Limite de conexões atingido.");
        }
    }
}
