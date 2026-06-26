using System;
using System.Collections.Generic;
using LiteNetLib;
using Silk.NET.Maths;
using ERus.Engine.Core;
using ERus.Engine.ECS;
using ERus.Engine.Network.Core;
using ERus.Engine.Scripting;
using ERus.Engine.Modules;
using ERus.Engine.Network.Packets.State;
using ERus.Engine.Network.Packets.Events;
using System.Collections.Concurrent;

namespace ERus.Engine.Network.Replication;

public class EntityReplicationSystem : BaseSystem
{
    private readonly NetworkTransport _transport;
    private readonly NetworkPacketDispatcher _dispatcher;
    private readonly ERus.Engine.Core.Engine _engine;
    private readonly NetworkIdentityMap _identityMap;

    private uint _currentTick = 0;
    private Dictionary<int, uint> _lastEntityTicks = new Dictionary<int, uint>();
    private ConcurrentQueue<(string hash, string path)> _completedDownloads = new();

    public EntityReplicationSystem(Registry registry, ERus.Engine.Core.Engine engine, NetworkTransport transport, NetworkPacketDispatcher dispatcher, NetworkIdentityMap identityMap) : base(registry)
    {
        _engine = engine;
        _transport = transport;
        _dispatcher = dispatcher;
        _identityMap = identityMap;
        
        RegisterPackets();

        var networkModule = _engine.GetModule<NetworkModule>();
        if (networkModule?.NetworkManager?.AssetSync != null)
        {
            networkModule.NetworkManager.AssetSync.OnAssetDownloaded += (hash, path) => 
            {
                _completedDownloads.Enqueue((hash, path));
            };
        }
    }

    public override void Update(double deltaTime)
    {
        while (_completedDownloads.TryDequeue(out var downloaded))
        {
            foreach (var entity in Registry.GetLivingEntities())
            {
                if (Registry.HasComponent<MeshComponent>(entity))
                {
                    ref var mesh = ref Registry.GetComponent<MeshComponent>(entity);
                    if (mesh.AssetHash == downloaded.hash)
                    {
                        var guid = _engine.AssetDatabase.GetGuidByPath(downloaded.path);
                        if (guid.HasValue)
                        {
                            mesh.AssetGuid = guid.Value;
                            mesh.Type = PrimitiveMeshType.None; // Remove Placeholder
                        }
                    }
                }
            }
        }

        // Broadcaster de Movimentação (Dirty Flags)
        if (_transport.IsHost)
        {
            foreach (var entity in Registry.GetLivingEntities())
            {
                if (Registry.HasComponent<TransformComponent>(entity) && Registry.HasComponent<NetworkIdentityComponent>(entity))
                {
                    ref var transform = ref Registry.GetComponent<TransformComponent>(entity);
                    if (transform.IsDirty)
                    {
                        var netIdComp = Registry.GetComponent<NetworkIdentityComponent>(entity);
                        SendTransform(netIdComp.NetworkId, transform.Position, transform.Rotation, transform.Scale);
                        transform.IsDirty = false;
                    }
                }
            }
        }

        // Interpolador de Movimento (Anti-Jitter)
        float lerpSpeed = 15f;
        foreach (var entity in Registry.GetLivingEntities())
        {
            if (Registry.HasComponent<TransformComponent>(entity) && Registry.HasComponent<NetworkInterpolationComponent>(entity))
            {
                ref var t = ref Registry.GetComponent<TransformComponent>(entity);
                ref var interp = ref Registry.GetComponent<NetworkInterpolationComponent>(entity);
                
                bool changed = false;
                if (interp.HasTargetPosition)
                {
                    t.Position += (interp.TargetPosition - t.Position) * ((float)deltaTime * lerpSpeed);
                    if ((interp.TargetPosition - t.Position).Length < 0.005f)
                    {
                        t.Position = interp.TargetPosition;
                        interp.HasTargetPosition = false;
                    }
                    changed = true;
                }
                
                if (interp.HasTargetRotation)
                {
                    t.Rotation += (interp.TargetRotation - t.Rotation) * ((float)deltaTime * lerpSpeed);
                    if ((interp.TargetRotation - t.Rotation).Length < 0.005f)
                    {
                        t.Rotation = interp.TargetRotation;
                        interp.HasTargetRotation = false;
                    }
                    changed = true;
                }
                
                if (interp.HasTargetScale)
                {
                    t.Scale += (interp.TargetScale - t.Scale) * ((float)deltaTime * lerpSpeed);
                    if ((interp.TargetScale - t.Scale).Length < 0.005f)
                    {
                        t.Scale = interp.TargetScale;
                        interp.HasTargetScale = false;
                    }
                    changed = true;
                }

                if (changed) t.IsDirty = false;
            }
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
            if (_identityMap.TryGetEntity(packet.NetworkId, out var entity))
            {
                if (Registry.HasComponent<TransformComponent>(entity))
                {
                    if (!Registry.HasComponent<NetworkInterpolationComponent>(entity))
                        Registry.AddComponent(entity, new NetworkInterpolationComponent());

                    ref var interp = ref Registry.GetComponent<NetworkInterpolationComponent>(entity);
                    
                    if ((packet.UpdateFlags & 1) != 0) 
                    {
                        interp.TargetPosition = new Vector3D<float>(packet.Position.X, packet.Position.Y, packet.Position.Z);
                        interp.HasTargetPosition = true;
                    }
                    if ((packet.UpdateFlags & 2) != 0) 
                    {
                        interp.TargetRotation = new Vector3D<float>(packet.Rotation.X, packet.Rotation.Y, packet.Rotation.Z);
                        interp.HasTargetRotation = true;
                    }
                    if ((packet.UpdateFlags & 4) != 0) 
                    {
                        interp.TargetScale = new Vector3D<float>(packet.Scale.X, packet.Scale.Y, packet.Scale.Z);
                        interp.HasTargetScale = true;
                    }
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
            
            MeshComponent meshComp = new MeshComponent();
            if (!string.IsNullOrEmpty(packet.AssetHash))
            {
                meshComp.AssetHash = packet.AssetHash;
                string? localPath = _engine.GetModule<NetworkModule>()?.NetworkManager?.AssetSync?.GetFilePathByHash(packet.AssetHash);
                if (!string.IsNullOrEmpty(localPath))
                {
                    var guid = _engine.AssetDatabase.GetGuidByPath(localPath);
                    if (guid.HasValue)
                    {
                        meshComp.AssetGuid = guid.Value;
                        meshComp.Type = PrimitiveMeshType.None;
                    }
                    else
                    {
                        meshComp.Type = PrimitiveMeshType.Cube;
                    }
                }
                else
                {
                    meshComp.Type = PrimitiveMeshType.Cube; // Placeholder
                }
            }
            else if (packet.MeshType > 0)
            {
                meshComp.Type = (PrimitiveMeshType)packet.MeshType;
            }
            
            if (meshComp.Type != PrimitiveMeshType.None || meshComp.AssetGuid != Guid.Empty)
                Registry.AddComponent(entity, meshComp);

            _identityMap.Map(packet.NetworkId, entity);
        });

        _dispatcher.SubscribeReusable<UpdateMeshPacket>((packet, peer) =>
        {
            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.ReliableOrdered);
            
            if (_identityMap.TryGetEntity(packet.NetworkId, out var entity))
            {
                MeshComponent meshComp = Registry.HasComponent<MeshComponent>(entity) 
                                         ? Registry.GetComponent<MeshComponent>(entity) 
                                         : new MeshComponent();

                meshComp.AssetHash = packet.AssetHash;
                
                if (!string.IsNullOrEmpty(packet.AssetHash))
                {
                    string? localPath = _engine.GetModule<NetworkModule>()?.NetworkManager?.AssetSync?.GetFilePathByHash(packet.AssetHash);
                    if (!string.IsNullOrEmpty(localPath))
                    {
                        var guid = _engine.AssetDatabase.GetGuidByPath(localPath);
                        if (guid.HasValue)
                        {
                            meshComp.AssetGuid = guid.Value;
                            meshComp.Type = PrimitiveMeshType.None;
                        }
                        else
                        {
                            meshComp.AssetGuid = Guid.Empty;
                            meshComp.Type = PrimitiveMeshType.Cube; // Placeholder
                        }
                    }
                    else
                    {
                        meshComp.AssetGuid = Guid.Empty;
                        meshComp.Type = PrimitiveMeshType.Cube; // Placeholder
                    }
                }
                else
                {
                    meshComp.Type = (PrimitiveMeshType)packet.MeshType;
                    meshComp.AssetGuid = Guid.Empty;
                }

                if (Registry.HasComponent<MeshComponent>(entity))
                    Registry.GetComponent<MeshComponent>(entity) = meshComp;
                else
                    Registry.AddComponent(entity, meshComp);
            }
        });

        _dispatcher.SubscribeReusable<LockPacket>((packet, peer) =>
        {
            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.ReliableOrdered);
            
            // Aplicar no ECS
            if (_identityMap.TryGetEntity(packet.NetworkId, out var entity))
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
            if (_identityMap.TryGetEntity(packet.NetworkId, out var entity))
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
            if (_identityMap.TryGetEntity(packet.NetworkId, out var entity))
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
            if (_identityMap.TryGetEntity(packet.NetworkId, out var entity))
            {
                Registry.DestroyEntity(entity);
                _identityMap.Remove(packet.NetworkId);
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
        _dispatcher.SubscribeReusable<UpdateCameraPacket>((packet, peer) =>
        {
            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.ReliableOrdered);
            
            if (_identityMap.TryGetEntity(packet.NetworkId, out var entity))
            {
                var cam = Registry.HasComponent<CameraComponent>(entity) ? Registry.GetComponent<CameraComponent>(entity) : new CameraComponent();
                cam.FieldOfView = packet.FieldOfView;
                cam.IsPrimary = packet.IsPrimary;
                cam.NearClip = packet.NearClip;
                cam.FarClip = packet.FarClip;
                
                if (Registry.HasComponent<CameraComponent>(entity))
                    Registry.GetComponent<CameraComponent>(entity) = cam;
                else
                    Registry.AddComponent(entity, cam);
            }
        });

        _dispatcher.SubscribeReusable<UpdatePhysicsPacket>((packet, peer) =>
        {
            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.ReliableOrdered);
            
            if (_identityMap.TryGetEntity(packet.NetworkId, out var entity))
            {
                var rb = Registry.HasComponent<RigidBodyComponent>(entity) ? Registry.GetComponent<RigidBodyComponent>(entity) : new RigidBodyComponent();
                rb.Mass = packet.Mass;
                rb.LinearDrag = packet.LinearDrag;
                rb.AngularDrag = packet.AngularDrag;
                rb.UseGravity = packet.UseGravity;
                rb.IsKinematic = packet.IsKinematic;
                rb.Constraints = (RigidbodyConstraints)packet.Constraints;
                
                if (Registry.HasComponent<RigidBodyComponent>(entity))
                    Registry.GetComponent<RigidBodyComponent>(entity) = rb;
                else
                    Registry.AddComponent(entity, rb);
            }
        });

        _dispatcher.SubscribeReusable<UpdateScriptPacket>((packet, peer) =>
        {
            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.ReliableOrdered);
            
            if (_identityMap.TryGetEntity(packet.NetworkId, out var entity))
            {
                var sc = Registry.HasComponent<ScriptComponent>(entity) ? Registry.GetComponent<ScriptComponent>(entity) : new ScriptComponent();
                sc.Scripts.Clear();
                foreach(var s in packet.Scripts)
                {
                    sc.Scripts.Add(new ScriptData {
                        ScriptTypeName = s.ScriptTypeName,
                        FieldValues = new System.Collections.Generic.Dictionary<string, string>(s.FieldValues)
                    });
                }
                
                if (Registry.HasComponent<ScriptComponent>(entity))
                    Registry.GetComponent<ScriptComponent>(entity) = sc;
                else
                    Registry.AddComponent(entity, sc);
            }
        });

        _dispatcher.SubscribeReusable<LoadScenePacket>((packet, peer) =>
        {
            if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, peer, DeliveryMethod.ReliableOrdered);
            
            if (!_transport.IsHost)
            {
                var ecs = _engine.GetModule<ERus.Engine.Modules.ECSModule>();
                if (ecs != null)
                {
                    ecs.ActiveScene.Clear();
                    _identityMap.ClearLocalMap();
                    ConsoleLog.Log($"[Rede] Host iniciou carregamento da cena {packet.SceneName}. Cena limpa localmente.");
                }
            }
        });

        // Assina download de assets
        var assetSync = _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.NetworkManager?.AssetSync;
        if (assetSync != null)
        {
            assetSync.OnAssetDownloaded += (hash, path) =>
            {
                _completedDownloads.Enqueue((hash, path));
                ConsoleLog.Log($"[Rede] Asset baixado e enfileirado para swap de malha: {hash}");
            };
        }
    }

    public void SendTransform(int networkId, Vector3D<float> position, Vector3D<float> rotation, Vector3D<float> scale, byte updateFlags = 7)
    {
        _currentTick++;
        var packet = new TransformPacket { NetworkId = networkId, Position = position, Rotation = rotation, Scale = scale, Tick = _currentTick, UpdateFlags = updateFlags };
        if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, null, DeliveryMethod.Sequenced, 1);
        else _dispatcher.SendToServer(packet, DeliveryMethod.Sequenced, 1);
    }

    public void SendTransformToPeer(NetPeer peer, int networkId, Vector3D<float> position, Vector3D<float> rotation, Vector3D<float> scale, byte updateFlags = 7)
    {
        _currentTick++;
        var packet = new TransformPacket { NetworkId = networkId, Position = position, Rotation = rotation, Scale = scale, Tick = _currentTick, UpdateFlags = updateFlags };
        _dispatcher.SendToPeer(peer, packet, DeliveryMethod.ReliableOrdered);
    }

    public void SendSpawn(int networkId, string tag, int meshType, string assetHash = "")
    {
        var packet = new SpawnEntityPacket { NetworkId = networkId, Tag = tag, MeshType = meshType, AssetHash = assetHash };
        if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, null, DeliveryMethod.ReliableOrdered);
        else _dispatcher.SendToServer(packet, DeliveryMethod.ReliableOrdered);
    }

    public void SendSpawnToPeer(NetPeer peer, int networkId, string tag, int meshType, string assetHash = "")
    {
        var packet = new SpawnEntityPacket { NetworkId = networkId, Tag = tag, MeshType = meshType, AssetHash = assetHash };
        _dispatcher.SendToPeer(peer, packet, DeliveryMethod.ReliableOrdered);
    }

    public void SendUpdateMesh(int networkId, int meshType, string assetHash = "")
    {
        var packet = new UpdateMeshPacket { NetworkId = networkId, MeshType = meshType, AssetHash = assetHash };
        if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, null, DeliveryMethod.ReliableOrdered);
        else _dispatcher.SendToServer(packet, DeliveryMethod.ReliableOrdered);
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
