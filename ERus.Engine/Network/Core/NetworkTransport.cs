using System;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;

namespace ERus.Engine.Network.Core;

public class NetworkTransport : INetEventListener
{
    private NetManager? _netManager;

    public bool IsHost { get; private set; }
    public int MyUserId { get; private set; }

    // Eventos
    public Action<NetPeer>? OnPeerConnectedEvent;
    public Action<NetPeer, DisconnectInfo>? OnPeerDisconnectedEvent;
    public Action<NetPeer, NetPacketReader, byte, DeliveryMethod>? OnNetworkReceiveEvent;

    public void InitializeAsHost(int port)
    {
        IsHost = true;
        MyUserId = 0; // O Host tem sempre a autoridade máxima (ID 0)
        _netManager = new NetManager(this);
        _netManager.Start(port);
        Console.WriteLine($"[Network] Host iniciado na porta {port}");
    }

    public void InitializeAsClient(string ip, int port)
    {
        IsHost = false;
        MyUserId = new Random().Next(1, 1000); // Geramos ID de client aleatório
        _netManager = new NetManager(this);
        _netManager.Start();
        _netManager.Connect(ip, port, "ERusKeys");
        Console.WriteLine($"[Network] Client conectando a {ip}:{port}...");
    }

    public void PollEvents() => _netManager?.PollEvents();
    public void Stop() => _netManager?.Stop();

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
            request.AcceptIfKey("ERusKeys");
        else
            request.Reject();
    }
}
