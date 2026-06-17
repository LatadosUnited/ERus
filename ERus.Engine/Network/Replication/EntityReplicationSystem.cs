using System;
using System.Collections.Generic;
using LiteNetLib;
using Silk.NET.Maths;
using ERus.Engine.Core;
using ERus.Engine.ECS;
using ERus.Engine.Network.Core;
using ERus.Engine.Scripting;

namespace ERus.Engine.Network.Replication;

public class EntityReplicationSystem : BaseSystem
{
    private readonly NetworkTransport _transport;
    private readonly NetworkPacketDispatcher _dispatcher;
    private readonly ERus.Engine.Core.Engine _engine;

    private uint _currentTick = 0;
    private Dictionary<int, uint> _lastEntityTicks = new Dictionary<int, uint>();
    
    // Mapa local de Rede para ECS
    private readonly Dictionary<int, Entity> _networkToLocalMap = new Dictionary<int, Entity>();
    private int _nextNetworkId = 1;

    public EntityReplicationSystem(Registry registry, ERus.Engine.Core.Engine engine, NetworkTransport transport, NetworkPacketDispatcher dispatcher) : base(registry)
    {
        _engine = engine;
        _transport = transport;
        _dispatcher = dispatcher;
        
        RegisterPackets();
        
        _transport.OnPeerConnectedEvent += OnPeerConnected;
    }

    public override void Update(double deltaTime)
    {
        // Neste sistema podemos fazer checks periódicos para enviar transforms
        // Por agora, o Envio é driven por eventos e Input, mas aqui é o lugar certo para 
        // tick-based netcode (ex: varrer Transforms que estão dirty e enviar packets)
    }

    public int AssignNetworkId(Entity entity)
    {
        int netId = _nextNetworkId++;
        _networkToLocalMap[netId] = entity;
        
        if (!Registry.HasComponent<NetworkIdentityComponent>(entity))
            Registry.AddComponent(entity, new NetworkIdentityComponent { NetworkId = netId, LockUserId = -1 });
            
        return netId;
    }

    public void ClearLocalMap()
    {
        _networkToLocalMap.Clear();
        _nextNetworkId = 1;
    }

    public int GetNetworkId(Entity entity)
    {
        if (Registry.HasComponent<NetworkIdentityComponent>(entity))
        {
            return Registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
        }
        return -1;
    }

    private void OnPeerConnected(NetPeer peer)
    {
        if (_transport.IsHost)
        {
            // Enviar o estado completo do mundo para quem acabou de entrar
            foreach (var kvp in _networkToLocalMap)
            {
                int netId = kvp.Key;
                Entity entity = kvp.Value;

                string tag = "Entity";
                if (Registry.HasComponent<TagComponent>(entity))
                    tag = Registry.GetComponent<TagComponent>(entity).Name;

                int meshType = 0;
                if (Registry.HasComponent<MeshComponent>(entity))
                    meshType = (int)Registry.GetComponent<MeshComponent>(entity).Type;

                // 1. Spawna a entidade
                SendSpawnToPeer(peer, netId, tag, meshType);

                // 2. Sincroniza a posição
                if (Registry.HasComponent<TransformComponent>(entity))
                {
                    var t = Registry.GetComponent<TransformComponent>(entity);
                    SendTransformToPeer(peer, netId, t.Position, t.Rotation, t.Scale);
                }

                // 3. Sincroniza o estado de Lock
                if (Registry.HasComponent<NetworkIdentityComponent>(entity))
                {
                    var netIdComp = Registry.GetComponent<NetworkIdentityComponent>(entity);
                    if (netIdComp.LockUserId != -1)
                    {
                        SendLockToPeer(peer, netId, netIdComp.LockUserId);
                    }
                }
            }
        }
        else
        {
            // Cliente conectou com sucesso: Limpa a cena local para receber os objetos do Host
            var ecs = _engine.GetModule<ERus.Engine.Modules.ECSModule>();
            if (ecs != null)
            {
                ecs.ActiveScene.Clear();
            }
            ClearLocalMap();
            ConsoleLog.Log("[Rede] Cena local limpa para sincronização com o Host.");
        }
    }

    private void RegisterPackets()
    {
        _dispatcher.SubscribeReusable<TransformPacket>((packet, peer) =>
        {
            if (_lastEntityTicks.TryGetValue(packet.NetworkId, out uint lastTick))
            {
                if (packet.Tick <= lastTick && lastTick - packet.Tick < 1000000)
                    return; // Drop old packet (Jitter fix)
            }
            _lastEntityTicks[packet.NetworkId] = packet.Tick;

            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.Unreliable);
            
            // Aplicar no ECS
            if (_networkToLocalMap.TryGetValue(packet.NetworkId, out var entity))
            {
                if (Registry.HasComponent<TransformComponent>(entity))
                {
                    ref var t = ref Registry.GetComponent<TransformComponent>(entity);
                    if ((packet.UpdateFlags & 1) != 0) t.Position = new Vector3D<float>(packet.Position.X, packet.Position.Y, packet.Position.Z);
                    if ((packet.UpdateFlags & 2) != 0) t.Rotation = new Vector3D<float>(packet.Rotation.X, packet.Rotation.Y, packet.Rotation.Z);
                    if ((packet.UpdateFlags & 4) != 0) t.Scale = new Vector3D<float>(packet.Scale.X, packet.Scale.Y, packet.Scale.Z);
                }
            }
        });

        _dispatcher.SubscribeReusable<SpawnEntityPacket>((packet, peer) =>
        {
            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.ReliableOrdered);
            
            // Aplicar no ECS
            var entity = Registry.CreateEntity();
            Registry.AddComponent(entity, new NetworkIdentityComponent { NetworkId = packet.NetworkId, LockUserId = -1 });
            Registry.AddComponent(entity, new TransformComponent());
            Registry.AddComponent(entity, new TagComponent { Name = packet.Tag });
            
            if (packet.MeshType > 0)
                Registry.AddComponent(entity, new MeshComponent { Type = (PrimitiveMeshType)packet.MeshType });

            _networkToLocalMap[packet.NetworkId] = entity;
        });

        _dispatcher.SubscribeReusable<LockPacket>((packet, peer) =>
        {
            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.ReliableOrdered);
            
            // Aplicar no ECS
            if (_networkToLocalMap.TryGetValue(packet.NetworkId, out var entity))
            {
                if (Registry.HasComponent<NetworkIdentityComponent>(entity))
                {
                    ref var netIdentity = ref Registry.GetComponent<NetworkIdentityComponent>(entity);
                    netIdentity.LockUserId = packet.UserId;
                }
            }
        });

        _dispatcher.SubscribeReusable<UnlockPacket>((packet, peer) =>
        {
            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.ReliableOrdered);
            
            // Aplicar no ECS
            if (_networkToLocalMap.TryGetValue(packet.NetworkId, out var entity))
            {
                if (Registry.HasComponent<NetworkIdentityComponent>(entity))
                {
                    ref var netIdentity = ref Registry.GetComponent<NetworkIdentityComponent>(entity);
                    netIdentity.LockUserId = -1;
                }
            }
        });

        _dispatcher.SubscribeReusable<RenameEntityPacket>((packet, peer) =>
        {
            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.ReliableOrdered);
            
            // Aplicar no ECS
            if (_networkToLocalMap.TryGetValue(packet.NetworkId, out var entity))
            {
                if (Registry.HasComponent<TagComponent>(entity))
                {
                    ref var tagComp = ref Registry.GetComponent<TagComponent>(entity);
                    tagComp.Name = packet.NewTag;
                }
            }
        });

        _dispatcher.SubscribeReusable<DestroyEntityPacket>((packet, peer) =>
        {
            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.ReliableOrdered);
            
            _lastEntityTicks.Remove(packet.NetworkId);
            
            // Aplicar no ECS
            if (_networkToLocalMap.TryGetValue(packet.NetworkId, out var entity))
            {
                Registry.DestroyEntity(entity);
                _networkToLocalMap.Remove(packet.NetworkId);
            }
        });

        _dispatcher.SubscribeReusable<EngineStatePacket>((packet, peer) =>
        {
            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.ReliableOrdered);
            
            if (!_transport.IsHost)
            {
                var targetState = (ERus.Engine.Core.EngineState)packet.State;
                if (_engine.State != targetState)
                {
                    string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.Environment.CurrentDirectory, "Assets/_temp_play.scene"));
                    var ecs = _engine.GetModule<ERus.Engine.Modules.ECSModule>();
                    
                    if (targetState == ERus.Engine.Core.EngineState.Play && _engine.State == ERus.Engine.Core.EngineState.Edit)
                    {
                        if (ecs != null) ERus.Engine.ECS.SceneSerializer.SaveScene(path, ecs.ActiveScene);
                    }
                    else if (targetState == ERus.Engine.Core.EngineState.Edit && _engine.State != ERus.Engine.Core.EngineState.Edit)
                    {
                        if (ecs != null) ERus.Engine.ECS.SceneSerializer.LoadScene(path, ecs.ActiveScene);
                    }
                    
                    _engine.State = targetState;
                    ConsoleLog.Log($"[Rede] Estado da Engine sincronizado para: {targetState}");
                }
            }
        });
    }

    public void SendTransform(int networkId, Vector3D<float> position, Vector3D<float> rotation, Vector3D<float> scale, byte updateFlags = 7)
    {
        _currentTick++;
        var packet = new TransformPacket { NetworkId = networkId, Position = position, Rotation = rotation, Scale = scale, Tick = _currentTick, UpdateFlags = updateFlags };
        if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, null, DeliveryMethod.Unreliable);
        else _dispatcher.SendToServer(packet, DeliveryMethod.Unreliable);
    }

    public void SendTransformToPeer(NetPeer peer, int networkId, Vector3D<float> position, Vector3D<float> rotation, Vector3D<float> scale, byte updateFlags = 7)
    {
        _currentTick++;
        var packet = new TransformPacket { NetworkId = networkId, Position = position, Rotation = rotation, Scale = scale, Tick = _currentTick, UpdateFlags = updateFlags };
        _dispatcher.SendToPeer(peer, packet, DeliveryMethod.ReliableOrdered);
    }

    public void SendSpawn(int networkId, string tag, int meshType)
    {
        var packet = new SpawnEntityPacket { NetworkId = networkId, Tag = tag, MeshType = meshType };
        if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, null, DeliveryMethod.ReliableOrdered);
        else _dispatcher.SendToServer(packet, DeliveryMethod.ReliableOrdered);
    }

    public void SendSpawnToPeer(NetPeer peer, int networkId, string tag, int meshType)
    {
        var packet = new SpawnEntityPacket { NetworkId = networkId, Tag = tag, MeshType = meshType };
        _dispatcher.SendToPeer(peer, packet, DeliveryMethod.ReliableOrdered);
    }

    public void SendLockToPeer(NetPeer peer, int networkId, int userId)
    {
        var packet = new LockPacket { NetworkId = networkId, UserId = userId };
        _dispatcher.SendToPeer(peer, packet, DeliveryMethod.ReliableOrdered);
    }

    public void RequestLock(int networkId)
    {
        var packet = new LockPacket { NetworkId = networkId, UserId = _transport.MyUserId };
        if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, null, DeliveryMethod.ReliableOrdered);
        else _dispatcher.SendToServer(packet, DeliveryMethod.ReliableOrdered);
    }

    public void SendUnlock(int networkId)
    {
        var packet = new UnlockPacket { NetworkId = networkId };
        if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, null, DeliveryMethod.ReliableOrdered);
        else _dispatcher.SendToServer(packet, DeliveryMethod.ReliableOrdered);
    }

    public void SendRename(int networkId, string newTag)
    {
        var packet = new RenameEntityPacket { NetworkId = networkId, NewTag = newTag };
        if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, null, DeliveryMethod.ReliableOrdered);
        else _dispatcher.SendToServer(packet, DeliveryMethod.ReliableOrdered);
    }

    public void SendDestroy(int networkId)
    {
        var packet = new DestroyEntityPacket { NetworkId = networkId };
        if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, null, DeliveryMethod.ReliableOrdered);
        else _dispatcher.SendToServer(packet, DeliveryMethod.ReliableOrdered);

        _lastEntityTicks.Remove(networkId); // Cleanup locally
    }

    public void SendEngineState(byte state)
    {
        var packet = new EngineStatePacket { State = state };
        if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, null, DeliveryMethod.ReliableOrdered);
        else _dispatcher.SendToServer(packet, DeliveryMethod.ReliableOrdered);
    }
}
