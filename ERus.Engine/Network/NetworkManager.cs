using System;
using LiteNetLib;
using ERus.Engine.Network.Core;
using ERus.Engine.Network.Replication;
using ERus.Engine.Network.Packets.Assets;
using ERus.Engine.Network.Collaboration;
using ERus.Engine.Modules;
using ERus.Engine.ECS;
using ERus.Engine.Scripting;

namespace ERus.Engine.Network;

public class NetworkManager
{
    private readonly ERus.Engine.Core.Engine _engine;

    public NetworkTransport Transport { get; }
    public NetworkPacketDispatcher Dispatcher { get; }
    public AssetSyncManager AssetSync { get; }
    public NetworkIdentityMap IdentityMap { get; }
    public WorldStateSynchronizer WorldSynchronizer { get; }
    public CollaboratorPresenceManager Presence { get; } = new();
    public string MyUsername { get; set; } = "Dev_" + Environment.UserName;

    public NetworkManager(ERus.Engine.Core.Engine engine)
    {
        _engine = engine;
        Transport = new NetworkTransport();
        Dispatcher = new NetworkPacketDispatcher(Transport);
        AssetSync = new AssetSyncManager(this);
        IdentityMap = new NetworkIdentityMap();
        WorldSynchronizer = new WorldStateSynchronizer(engine, Transport, Dispatcher, IdentityMap);

        Dispatcher.SubscribeReusable<AssetAnnouncePacket>((packet, peer) => AssetSync.OnAssetAnnouncedReceived(packet));
        
        Transport.OnPeerDisconnectedEvent += (peer, info) =>
        {
            int disconnectedUserId = Transport.GetUserIdForPeer(peer.Id);
            if (disconnectedUserId == NetworkTransport.UnknownUserId) return;

            Presence.RemoveCollaborator(disconnectedUserId);

            if (Transport.IsHost)
            {
                ReleaseOrphanLocks(disconnectedUserId);
            }
        };
    }

    public bool IsHost => Transport.IsHost;
    public bool IsClient => Transport.IsClient;
    public bool IsConnected => Transport.IsConnected;
    public int MyUserId => Transport.MyUserId;
    public int ConnectedPeersCount => Transport.ConnectedPeersCount;

    public void InitializeAsHost(int port, int tcpPort = -1, string sessionToken = "") 
    {
        int finalTcpPort = tcpPort == -1 ? port + 1 : tcpPort;
        Transport.InitializeAsHost(port, sessionToken);
        AssetSync.StartServer(finalTcpPort);
    }

    public void InitializeAsServer(int port, int tcpPort = -1, string sessionToken = "") 
    {
        int finalTcpPort = tcpPort == -1 ? port + 1 : tcpPort;
        Transport.InitializeAsServer(port, sessionToken);
        AssetSync.StartServer(finalTcpPort);
    }

    public void InitializeAsClient(string ip, int port, int tcpPort = -1, string sessionToken = "") 
    {
        int finalTcpPort = tcpPort == -1 ? port + 1 : tcpPort;
        Transport.InitializeAsClient(ip, port, sessionToken);
        AssetSync.SetupClient(ip, finalTcpPort);
    }

    /// <summary>
    /// Destrava tudo o que o usuário caído mantinha bloqueado. Sem isto, a entidade fica
    /// ineditável para os demais até o fim da sessão.
    /// </summary>
    public void ReleaseOrphanLocks(int disconnectedUserId)
    {
        // -1 é "sem dono": liberar por esse valor destravaria a cena inteira.
        if (disconnectedUserId == -1 || disconnectedUserId == NetworkTransport.UnknownUserId) return;

        var ecs = _engine.GetModule<ECSModule>();
        if (ecs == null) return;

        var registry = ecs.ActiveScene.Registry;
        var replication = ecs.GetSystem<EntityReplicationSystem>();
        var living = registry.GetLivingEntities();

        foreach (var entity in living)
        {
            if (!registry.HasComponent<NetworkIdentityComponent>(entity)) continue;

            ref var identity = ref registry.GetComponent<NetworkIdentityComponent>(entity);
            if (identity.LockUserId == disconnectedUserId)
            {
                int netId = identity.NetworkId;
                identity.LockUserId = -1;
                replication?.SendUnlock(netId);
                ConsoleLog.Log($"[Rede] Lock órfão liberado automaticamente para entidade #{netId} (usuário #{disconnectedUserId} desconectou).");
            }
        }
    }

    public void PollEvents() => Transport.PollEvents();
    
    public void Stop() 
    {
        AssetSync.StopServer();
        Transport.Stop();
        Presence.Clear();
    }

    public void SendAssetAnnounce(AssetAnnouncePacket packet)
    {
        Dispatcher.SendToAllExcept(packet, null, DeliveryMethod.ReliableOrdered);
    }
}
