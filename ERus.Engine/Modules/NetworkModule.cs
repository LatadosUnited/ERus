using ERus.Engine.Core;
using ERus.Engine.Network;
using ERus.Engine.Network.Replication;
using ERus.Engine.Scripting;
using System;
using ERus.Engine.ECS;

namespace ERus.Engine.Modules;

/// <summary>
/// Módulo que lida com o ciclo de vida do NetworkManager.
/// É responsável por ouvir o socket, processar pacotes que chegam e enviar os que foram acumulados no frame.
/// </summary>
public class NetworkModule : IEngineModule
{
    public NetworkManager NetworkManager { get; private set; }

    public EntityReplicationSystem? Replication
    {
        get
        {
            var ecs = _engine?.GetModule<ECSModule>();
            return ecs?.GetSystem<EntityReplicationSystem>();
        }
    }

    private Core.Engine _engine;

    private string? _pendingConnectIp;
    private int _pendingConnectPort;
    private string? _pendingToken;
    private string? _pendingProjectId;

    public void SetPendingRemoteConnection(string ip, int port, string token, string projectId)
    {
        _pendingConnectIp = ip;
        _pendingConnectPort = port;
        _pendingToken = token;
        _pendingProjectId = projectId;
    }

    public void Initialize(Core.Engine engine)
    {
        _engine = engine;
        // Criação única do gerenciador em estado vazio (Offline)
        NetworkManager = new NetworkManager(_engine);

        var ecs = _engine.GetModule<ECSModule>();
        if (ecs != null)
        {
            var replicationSystem = new EntityReplicationSystem(ecs.ActiveScene.Registry, _engine, NetworkManager.Transport, NetworkManager.Dispatcher, NetworkManager.IdentityMap);
            ecs.AddSystem(replicationSystem);
        }

        if (!string.IsNullOrEmpty(_pendingConnectIp))
        {
            StartClientWithAuth(_pendingConnectIp, _pendingConnectPort, _pendingToken ?? "", _pendingProjectId ?? "");
        }
    }

    public void StartHost(int port)
    {
        NetworkManager.Stop();
        
        try
        {
            NetworkManager.InitializeAsHost(port);
            ConsoleLog.Log($"[Rede] Host iniciado na porta {port}.");

            var ecs = _engine.GetModule<ECSModule>();
            var replicationSystem = ecs?.GetSystem<EntityReplicationSystem>();

            if (replicationSystem != null && ecs != null)
            {
                NetworkManager.IdentityMap.ClearLocalMap();
                
                // Retrospectiva: Varre todas as entidades já existentes na cena e atribui um ID de Rede
                var allEntities = ecs.ActiveScene.Registry.GetLivingEntities();
                foreach (var e in allEntities)
                {
                    if (!ecs.ActiveScene.Registry.HasComponent<NetworkIdentityComponent>(e))
                    {
                        NetworkManager.IdentityMap.AssignNetworkId(ecs.ActiveScene.Registry, e);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Rede] Erro ao iniciar Host: {ex.Message}");
        }
    }

    public void StartServer(int port)
    {
        NetworkManager.Stop();
        
        try
        {
            NetworkManager.InitializeAsServer(port);
            ConsoleLog.Log($"[Rede] Servidor Dedicado iniciado na porta {port}.");

            var ecs = _engine.GetModule<ECSModule>();
            var replicationSystem = ecs?.GetSystem<EntityReplicationSystem>();

            if (replicationSystem != null && ecs != null)
            {
                NetworkManager.IdentityMap.ClearLocalMap();
                
                // Atribui IDs de rede para todas as entidades iniciais carregadas na cena do servidor
                var allEntities = ecs.ActiveScene.Registry.GetLivingEntities();
                foreach (var e in allEntities)
                {
                    if (!ecs.ActiveScene.Registry.HasComponent<NetworkIdentityComponent>(e))
                    {
                        NetworkManager.IdentityMap.AssignNetworkId(ecs.ActiveScene.Registry, e);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Rede] Erro ao iniciar Servidor: {ex.Message}");
        }
    }

    public void StartClient(string ip, int port)
    {
        NetworkManager.Stop();
        
        try
        {
            NetworkManager.InitializeAsClient(ip, port);
            ConsoleLog.Log($"[Rede] Cliente tentando conectar em {ip}:{port}.");
            
            var ecs = _engine.GetModule<ECSModule>();
            var replicationSystem = ecs?.GetSystem<EntityReplicationSystem>();
            if (replicationSystem != null)
            {
                NetworkManager.IdentityMap.ClearLocalMap();
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Rede] Erro ao conectar: {ex.Message}");
        }
    }

    public void StartClientWithAuth(string ip, int port, string token, string projectId)
    {
        NetworkManager.Stop();
        
        try
        {
            Action<LiteNetLib.NetPeer>? onConnect = null;
            onConnect = (peer) => 
            {
                var authPacket = new ERus.Engine.Network.Packets.Auth.AuthRequestPacket 
                {
                    Token = token,
                    ProjectId = projectId
                };
                NetworkManager.Dispatcher.SendToPeer(peer, authPacket, LiteNetLib.DeliveryMethod.ReliableOrdered);
                ConsoleLog.Log($"[Rede] Conectado. Enviando pacote de Autenticação para {projectId}...");
                
                NetworkManager.Transport.OnPeerConnectedEvent -= onConnect;
            };

            NetworkManager.Transport.OnPeerConnectedEvent += onConnect;

            NetworkManager.InitializeAsClient(ip, port);
            ConsoleLog.Log($"[Rede] Cliente tentando conectar em {ip}:{port} para abrir o projeto {projectId}...");
            
            var ecs = _engine.GetModule<ECSModule>();
            var replicationSystem = ecs?.GetSystem<EntityReplicationSystem>();
            if (replicationSystem != null)
            {
                NetworkManager.IdentityMap.ClearLocalMap();
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Rede] Erro ao conectar: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        NetworkManager.Stop();
        
        var ecs = _engine.GetModule<ECSModule>();
        var replicationSystem = ecs?.GetSystem<EntityReplicationSystem>();
        if (replicationSystem != null)
        {
            NetworkManager.IdentityMap.ClearLocalMap();
        }
        
        ConsoleLog.Log("[Rede] Desconectado.");
    }

    public void Update(double deltaTime)
    {
        NetworkManager.PollEvents();
    }

    public void Render(double deltaTime)
    {
        // Rede é agnóstica à renderização visual
    }

    public void Dispose()
    {
        NetworkManager?.Stop();
    }
}
