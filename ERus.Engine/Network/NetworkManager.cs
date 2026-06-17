using System;
using LiteNetLib;
using ERus.Engine.Network.Core;

namespace ERus.Engine.Network;

public class NetworkManager
{
    public NetworkTransport Transport { get; }
    public NetworkPacketDispatcher Dispatcher { get; }
    public AssetSyncManager AssetSync { get; }

    public NetworkManager()
    {
        Transport = new NetworkTransport();
        Dispatcher = new NetworkPacketDispatcher(Transport);
        AssetSync = new AssetSyncManager(this);

        Dispatcher.SubscribeReusable<AssetAnnouncePacket>((packet, peer) => AssetSync.OnAssetAnnouncedReceived(packet));
    }

    public bool IsHost => Transport.IsHost;
    public int MyUserId => Transport.MyUserId;

    public void InitializeAsHost(int port, int tcpPort = -1) 
    {
        int finalTcpPort = tcpPort == -1 ? port + 1 : tcpPort;
        Transport.InitializeAsHost(port);
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
