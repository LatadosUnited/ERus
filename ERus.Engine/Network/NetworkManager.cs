using System;
using LiteNetLib;
using ERus.Engine.Network.Core;
using ERus.Engine.Network.Replication;
using ERus.Engine.Network.Packets.Assets;

namespace ERus.Engine.Network;

public class NetworkManager
{
    public NetworkTransport Transport { get; }
    public NetworkPacketDispatcher Dispatcher { get; }
    public AssetSyncManager AssetSync { get; }
    public NetworkIdentityMap IdentityMap { get; }
    public WorldStateSynchronizer WorldSynchronizer { get; }

    public NetworkManager(ERus.Engine.Core.Engine engine)
    {
        Transport = new NetworkTransport();
        Dispatcher = new NetworkPacketDispatcher(Transport);
        AssetSync = new AssetSyncManager(this);
        IdentityMap = new NetworkIdentityMap();
        WorldSynchronizer = new WorldStateSynchronizer(engine, Transport, Dispatcher, IdentityMap);

        Dispatcher.SubscribeReusable<AssetAnnouncePacket>((packet, peer) => AssetSync.OnAssetAnnouncedReceived(packet));
    }

    public bool IsHost => Transport.IsHost;
    public int MyUserId => Transport.MyUserId;
    public int ConnectedPeersCount => Transport.ConnectedPeersCount;

    public void InitializeAsHost(int port, int tcpPort = -1) 
    {
        int finalTcpPort = tcpPort == -1 ? port + 1 : tcpPort;
        Transport.InitializeAsHost(port);
        AssetSync.StartServer(finalTcpPort);
    }

    public void InitializeAsServer(int port, int tcpPort = -1) 
    {
        int finalTcpPort = tcpPort == -1 ? port + 1 : tcpPort;
        Transport.InitializeAsServer(port);
        AssetSync.StartServer(finalTcpPort);
    }

    public void InitializeAsClient(string ip, int port, int tcpPort = -1) 
    {
        int finalTcpPort = tcpPort == -1 ? port + 1 : tcpPort;
        Transport.InitializeAsClient(ip, port);
        AssetSync.SetupClient(ip, finalTcpPort);
    }

    public void PollEvents() => Transport.PollEvents();
    
    public void Stop() 
    {
        AssetSync.StopServer();
        Transport.Stop();
    }

    public void SendAssetAnnounce(AssetAnnouncePacket packet)
    {
        Dispatcher.SendToAllExcept(packet, null, DeliveryMethod.ReliableOrdered);
    }
}
