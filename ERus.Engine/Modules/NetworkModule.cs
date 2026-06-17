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

    public void Initialize(Core.Engine engine)
    {
        _engine = engine;
        // Criação única do gerenciador em estado vazio (Offline)
        NetworkManager = new NetworkManager();

        var ecs = _engine.GetModule<ECSModule>();
        if (ecs != null)
        {
            var replicationSystem = new EntityReplicationSystem(ecs.ActiveScene.Registry, _engine, NetworkManager.Transport, NetworkManager.Dispatcher);
            ecs.AddSystem(replicationSystem);
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
                replicationSystem.ClearLocalMap();
                
                // Retrospectiva: Varre todas as entidades já existentes na cena e atribui um ID de Rede
                var allEntities = ecs.ActiveScene.Registry.GetLivingEntities();
                foreach (var e in allEntities)
                {
                    if (!ecs.ActiveScene.Registry.HasComponent<NetworkIdentityComponent>(e))
                    {
                        replicationSystem.AssignNetworkId(e);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Rede] Erro ao iniciar Host: {ex.Message}");
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
                replicationSystem.ClearLocalMap();
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
            replicationSystem.ClearLocalMap();
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
