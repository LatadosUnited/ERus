using System;
using System.Threading;
using ERus.Engine.Core;
using ERus.Engine.ECS;
using ERus.Engine.Modules;
using ERus.Engine.Network;
using ERus.Engine.Network.Core;
using ERus.Engine.Network.Packets.Events;
using LiteNetLib;
using LiteNetLib.Utils;
using Xunit;

namespace ERus.Tests;

public class SessionSecurityAndOrphanLockTests
{
    [Fact]
    public void NetworkTransport_AcceptsConnection_WithMatchingSessionToken()
    {
        int port = 19530;
        string validToken = "ValidToken_" + Guid.NewGuid().ToString("N");

        var hostTransport = new NetworkTransport();
        var clientTransport = new NetworkTransport();

        bool hostAccepted = false;
        bool clientConnected = false;

        hostTransport.OnPeerConnectedEvent += peer => hostAccepted = true;
        clientTransport.OnPeerConnectedEvent += peer => clientConnected = true;

        try
        {
            hostTransport.InitializeAsHost(port, validToken);
            clientTransport.InitializeAsClient("127.0.0.1", port, validToken);

            for (int i = 0; i < 30; i++)
            {
                hostTransport.PollEvents();
                clientTransport.PollEvents();
                if (hostAccepted && clientConnected) break;
                Thread.Sleep(20);
            }

            Assert.True(hostAccepted, "O Host deveria ter aceito a conexão com token correspondente.");
            Assert.True(clientConnected, "O Cliente deveria ter conectado com sucesso.");
        }
        finally
        {
            clientTransport.Stop();
            hostTransport.Stop();
        }
    }

    [Fact]
    public void NetworkTransport_RejectsConnection_WithInvalidSessionToken()
    {
        int port = 19531;
        string serverToken = "CorrectServerToken";
        string invalidClientToken = "WrongToken";

        var hostTransport = new NetworkTransport();
        var clientTransport = new NetworkTransport();

        bool hostAccepted = false;

        hostTransport.OnPeerConnectedEvent += peer => hostAccepted = true;

        try
        {
            hostTransport.InitializeAsHost(port, serverToken);
            clientTransport.InitializeAsClient("127.0.0.1", port, invalidClientToken);

            for (int i = 0; i < 30; i++)
            {
                hostTransport.PollEvents();
                clientTransport.PollEvents();
                Thread.Sleep(20);
            }

            Assert.False(hostAccepted, "O Host NÃO deveria aceitar conexão com token inválido.");
        }
        finally
        {
            clientTransport.Stop();
            hostTransport.Stop();
        }
    }

    [Fact]
    public void NetworkManager_ReleaseOrphanLocks_ClearsLocksForDisconnectedUser()
    {
        var engine = new ERus.Engine.Core.Engine();
        var physics = new PhysicsModule();
        engine.AddModule(physics);
        physics.Initialize(engine);

        var ecs = new ECSModule();
        engine.AddModule(ecs);
        ecs.Initialize(engine);

        var netModule = new NetworkModule();
        engine.AddModule(netModule);
        netModule.Initialize(engine);

        var registry = ecs.ActiveScene.Registry;

        // Cria entidade 1 travada pelo usuário 999
        var entity1 = registry.CreateEntity();
        registry.AddComponent(entity1, new TransformComponent());
        registry.AddComponent(entity1, new TagComponent { Name = "LockedBy999" });
        registry.AddComponent(entity1, new NetworkIdentityComponent { NetworkId = 101, LockUserId = 999 });

        // Cria entidade 2 travada pelo usuário 888
        var entity2 = registry.CreateEntity();
        registry.AddComponent(entity2, new TransformComponent());
        registry.AddComponent(entity2, new TagComponent { Name = "LockedBy888" });
        registry.AddComponent(entity2, new NetworkIdentityComponent { NetworkId = 102, LockUserId = 888 });

        // Executa a liberação de locks órfãos para o usuário 999 que caiu
        netModule.NetworkManager.ReleaseOrphanLocks(999);

        // Valida que a entidade 1 foi destravada (LockUserId == -1)
        var identity1 = registry.GetComponent<NetworkIdentityComponent>(entity1);
        Assert.Equal(-1, identity1.LockUserId);

        // Valida que a entidade 2 permaneceu travada para o usuário 888
        var identity2 = registry.GetComponent<NetworkIdentityComponent>(entity2);
        Assert.Equal(888, identity2.LockUserId);
    }

    /// <summary>
    /// Cenário do roadmap: cliente derrubado no meio de um lock. Passa pelo caminho real
    /// (queda de peer -> OnPeerDisconnected -> resolução peer/usuário -> liberação),
    /// e não pela chamada direta a ReleaseOrphanLocks.
    /// </summary>
    [Fact]
    public void HostReleasesLock_WhenLockingClientDropsConnection()
    {
        int port = 19532;
        string token = "OrphanLockToken";

        var engine = new ERus.Engine.Core.Engine();
        var physics = new PhysicsModule();
        engine.AddModule(physics);
        physics.Initialize(engine);

        var ecs = new ECSModule();
        engine.AddModule(ecs);
        ecs.Initialize(engine);

        var netModule = new NetworkModule();
        engine.AddModule(netModule);
        netModule.Initialize(engine);

        var hostManager = netModule.NetworkManager;
        var clientTransport = new NetworkTransport();

        try
        {
            hostManager.Transport.InitializeAsHost(port, token);
            clientTransport.InitializeAsClient("127.0.0.1", port, token);

            Pump(hostManager.Transport, clientTransport, () => hostManager.ConnectedPeersCount > 0);
            Assert.True(hostManager.ConnectedPeersCount > 0, "Cliente deveria ter conectado ao Host.");

            // O cliente anuncia presença: é assim que o Host aprende o User ID por trás do peer.
            int clientUserId = clientTransport.MyUserId;
            SendPresence(clientTransport, clientUserId);

            Pump(hostManager.Transport, clientTransport,
                () => hostManager.Transport.GetUserIdForPeer(0) == clientUserId);

            // Entidade travada por esse cliente no momento da queda.
            var registry = ecs.ActiveScene.Registry;
            var locked = registry.CreateEntity();
            registry.AddComponent(locked, new TransformComponent());
            registry.AddComponent(locked, new TagComponent { Name = "EmEdicao" });
            registry.AddComponent(locked, new NetworkIdentityComponent { NetworkId = 4242, LockUserId = clientUserId });

            // Queda abrupta do cliente.
            clientTransport.Stop();

            Pump(hostManager.Transport, null,
                () => registry.GetComponent<NetworkIdentityComponent>(locked).LockUserId == -1,
                iterations: 200);

            var identity = registry.GetComponent<NetworkIdentityComponent>(locked);
            Assert.Equal(-1, identity.LockUserId);
        }
        finally
        {
            clientTransport.Stop();
            hostManager.Stop();
        }
    }

    /// <summary>
    /// Envia o pacote de presença sem montar um dispatcher no cliente. O cliente aqui é um
    /// peer mínimo: se ele assinasse a recepção, teria de saber ler o estado de mundo que o
    /// Host despeja em quem conecta, o que não é o que este teste verifica.
    /// </summary>
    private static void SendPresence(NetworkTransport client, int userId)
    {
        var peer = client.NetManager?.FirstPeer;
        Assert.NotNull(peer);

        var writer = new NetDataWriter();
        new NetPacketProcessor().Write(writer, new UserPresencePacket { UserId = userId, Username = "ClienteQueVaiCair" });
        peer!.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    /// <summary>Roda o loop de rede até a condição valer ou o orçamento de tentativas acabar.</summary>
    private static void Pump(NetworkTransport host, NetworkTransport? client, Func<bool> until, int iterations = 60)
    {
        for (int i = 0; i < iterations; i++)
        {
            host.PollEvents();
            client?.PollEvents();
            if (until()) return;
            Thread.Sleep(20);
        }
    }
}
