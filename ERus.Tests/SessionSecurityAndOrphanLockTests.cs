using System;
using System.Threading;
using ERus.Engine.Core;
using ERus.Engine.ECS;
using ERus.Engine.Modules;
using ERus.Engine.Network;
using ERus.Engine.Network.Core;
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
}
