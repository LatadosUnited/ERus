using LiteNetLib;
using ERus.Engine.ECS;
using ERus.Engine.Network.Core;
using ERus.Engine.Core;
using ERus.Engine.Scripting;
using ERus.Engine.Network.Packets.State;
using ERus.Engine.Network.Packets.Events;
using System.Linq;
using System.Collections.Generic;

namespace ERus.Engine.Network.Replication;

public class WorldStateSynchronizer
{
    private readonly Engine.Core.Engine _engine;
    private readonly NetworkTransport _transport;
    private readonly NetworkPacketDispatcher _dispatcher;
    private readonly NetworkIdentityMap _identityMap;

    public WorldStateSynchronizer(Engine.Core.Engine engine, NetworkTransport transport, NetworkPacketDispatcher dispatcher, NetworkIdentityMap identityMap)
    {
        _engine = engine;
        _transport = transport;
        _dispatcher = dispatcher;
        _identityMap = identityMap;

        _transport.OnPeerConnectedEvent += OnPeerConnected;
    }

    private void OnPeerConnected(NetPeer peer)
    {
        if (_transport.IsHost)
        {
            var ecs = _engine?.GetModule<ERus.Engine.Modules.ECSModule>();
            if (ecs == null) return;
            var registry = ecs.ActiveScene.Registry;

            // Enviar LoadScenePacket primeiro
            var loadScenePacket = new LoadScenePacket { SceneName = "SceneNamePlaceholder" };
            _dispatcher.SendToPeer(peer, loadScenePacket, DeliveryMethod.ReliableOrdered);

            // Anunciar Assets para download (TCP)
            var assetSync = _engine?.GetModule<ERus.Engine.Modules.NetworkModule>()?.NetworkManager?.AssetSync;
            if (assetSync != null)
            {
                HashSet<string> announcedHashes = new();
                foreach (var entity in registry.View<MeshComponent>())
                {
                    var meshComp = registry.GetComponent<MeshComponent>(entity);
                    if (!string.IsNullOrEmpty(meshComp.AssetHash) && announcedHashes.Add(meshComp.AssetHash))
                    {
                        string? path = ERus.Engine.Core.Engine.Instance?.AssetDatabase.GetPathByHash(meshComp.AssetHash);
                        if (!string.IsNullOrEmpty(path))
                        {
                            _ = assetSync.AnnounceAssetToPeerAsync(path, peer);
                        }
                    }
                }
            }

            // Enviar o estado completo do mundo para quem acabou de entrar
            foreach (var kvp in _identityMap.GetAllMappings())
            {
                int netId = kvp.Key;
                Entity entity = kvp.Value;

                string tag = "Entity";
                if (registry.HasComponent<TagComponent>(entity))
                    tag = registry.GetComponent<TagComponent>(entity).Name;

                int meshType = 0;
                string assetHash = string.Empty;
                if (registry.HasComponent<MeshComponent>(entity))
                {
                    var meshComp = registry.GetComponent<MeshComponent>(entity);
                    meshType = (int)meshComp.Type;
                    assetHash = meshComp.AssetHash ?? string.Empty;
                }

                // 1. Spawna a entidade
                var spawnPacket = new SpawnEntityPacket { NetworkId = netId, Tag = tag, MeshType = meshType, AssetHash = assetHash };
                _dispatcher.SendToPeer(peer, spawnPacket, DeliveryMethod.ReliableOrdered);

                // 2. Sincroniza a posição
                if (registry.HasComponent<TransformComponent>(entity))
                {
                    var t = registry.GetComponent<TransformComponent>(entity);
                    var transformPacket = new TransformPacket 
                    { 
                        NetworkId = netId, 
                        Position = t.Position, 
                        Rotation = t.Rotation, 
                        Scale = t.Scale, 
                        Tick = 0, 
                        UpdateFlags = 7 
                    };
                    _dispatcher.SendToPeer(peer, transformPacket, DeliveryMethod.ReliableOrdered);
                }

                // 3. Sincroniza o estado de Lock
                if (registry.HasComponent<NetworkIdentityComponent>(entity))
                {
                    var netIdComp = registry.GetComponent<NetworkIdentityComponent>(entity);
                    if (netIdComp.LockUserId != -1)
                    {
                        var lockPacket = new LockPacket { NetworkId = netId, UserId = netIdComp.LockUserId };
                        _dispatcher.SendToPeer(peer, lockPacket, DeliveryMethod.ReliableOrdered);
                    }
                }

                // 4. Sincroniza a Câmera
                if (registry.HasComponent<CameraComponent>(entity))
                {
                    var cam = registry.GetComponent<CameraComponent>(entity);
                    var camPacket = new UpdateCameraPacket 
                    {
                        NetworkId = netId,
                        FieldOfView = cam.FieldOfView,
                        IsPrimary = cam.IsPrimary,
                        NearClip = cam.NearClip,
                        FarClip = cam.FarClip
                    };
                    _dispatcher.SendToPeer(peer, camPacket, DeliveryMethod.ReliableOrdered);
                }

                // 5. Sincroniza Física (RigidBody)
                if (registry.HasComponent<RigidBodyComponent>(entity))
                {
                    var rb = registry.GetComponent<RigidBodyComponent>(entity);
                    var physPacket = new UpdatePhysicsPacket 
                    {
                        NetworkId = netId,
                        Mass = rb.Mass,
                        LinearDrag = rb.LinearDrag,
                        AngularDrag = rb.AngularDrag,
                        UseGravity = rb.UseGravity,
                        IsKinematic = rb.IsKinematic,
                        Constraints = (int)rb.Constraints
                    };
                    _dispatcher.SendToPeer(peer, physPacket, DeliveryMethod.ReliableOrdered);
                }

                // 6. Sincroniza Scripts
                if (registry.HasComponent<ScriptComponent>(entity))
                {
                    var scriptComp = registry.GetComponent<ScriptComponent>(entity);
                    var sDataList = new List<ScriptPacketData>();
                    foreach (var s in scriptComp.Scripts)
                    {
                        var sData = new ScriptPacketData
                        {
                            ScriptTypeName = s.ScriptTypeName,
                            FieldValues = s.FieldValues.ToDictionary(k => k.Key, v => v.Value)
                        };
                        sDataList.Add(sData);
                    }
                    var scriptPacket = new UpdateScriptPacket { NetworkId = netId, Scripts = sDataList.ToArray() };
                    _dispatcher.SendToPeer(peer, scriptPacket, DeliveryMethod.ReliableOrdered);
                }
            }
        }
        else
        {
            // Cliente conectou com sucesso: Limpa a cena local para receber os objetos do Host
            var ecs = _engine?.GetModule<ERus.Engine.Modules.ECSModule>();
            if (ecs != null)
            {
                ecs.ActiveScene.Clear();
            }
            _identityMap.ClearLocalMap();
            ConsoleLog.Log("[Rede] Cena local limpa para sincronização com o Host.");
        }
    }
}
