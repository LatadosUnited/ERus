using System;
using ERus.Engine.ECS;
using ERus.Engine.Network.Packets.Events;

namespace ERus.Engine.Network.Replication.Handlers;

/// <summary>
/// Ciclo de vida e identidade das entidades replicadas: spawn, destroy, rename e locks
/// de edição colaborativa (Temporal Locking).
/// </summary>
public sealed class EntityLifecycleHandler : IReplicationHandler
{
    public void Register(ReplicationContext ctx)
    {
        RegisterSpawn(ctx);
        RegisterDestroy(ctx);
        RegisterRename(ctx);
        RegisterLocks(ctx);
    }

    private static void RegisterSpawn(ReplicationContext ctx)
    {
        ctx.RegisterRelayed<SpawnEntityPacket>((packet, peer) =>
        {
            var entity = ctx.Registry.CreateEntity();
            ctx.Registry.AddComponent(entity, new NetworkIdentityComponent { NetworkId = packet.NetworkId, LockUserId = -1 });
            ctx.Registry.AddComponent(entity, new TransformComponent());
            ctx.Registry.AddComponent(entity, new TagComponent { Name = packet.Tag });

            var mesh = BuildSpawnMesh(ctx, packet);
            if (mesh.Type != PrimitiveMeshType.None || mesh.AssetGuid != Guid.Empty)
                ctx.Registry.AddComponent(entity, mesh);

            ctx.IdentityMap.Map(packet.NetworkId, entity);
        });
    }

    /// <summary>
    /// Monta o MeshComponent inicial. Se o asset referenciado ainda não chegou, a
    /// entidade nasce como cubo placeholder e o AssetSwapProcessor troca depois.
    /// </summary>
    private static MeshComponent BuildSpawnMesh(ReplicationContext ctx, SpawnEntityPacket packet)
    {
        var mesh = new MeshComponent();

        if (!string.IsNullOrEmpty(packet.AssetHash))
        {
            mesh.AssetHash = packet.AssetHash;

            var guid = ctx.ResolveGuidByAssetHash(packet.AssetHash);
            if (guid.HasValue)
            {
                mesh.AssetGuid = guid.Value;
                mesh.Type = PrimitiveMeshType.None;
            }
            else
            {
                mesh.Type = PrimitiveMeshType.Cube; // Placeholder até o download concluir
            }
        }
        else if (packet.MeshType > 0)
        {
            mesh.Type = (PrimitiveMeshType)packet.MeshType;
        }

        return mesh;
    }

    private static void RegisterDestroy(ReplicationContext ctx)
    {
        ctx.RegisterRelayed<DestroyEntityPacket>((packet, peer) =>
        {
            ctx.Ticks.Forget(packet.NetworkId);

            if (!ctx.TryGetEntity(packet.NetworkId, out var entity)) return;

            ctx.Registry.DestroyEntity(entity);
            ctx.IdentityMap.Remove(packet.NetworkId);
        });
    }

    private static void RegisterRename(ReplicationContext ctx)
    {
        ctx.RegisterRelayed<RenameEntityPacket>((packet, peer) =>
        {
            if (!ctx.TryGetEntity(packet.NetworkId, out var entity)) return;
            if (!ctx.Registry.HasComponentByType(entity, typeof(TagComponent))) return;

            ref var tag = ref ctx.Registry.GetComponent<TagComponent>(entity);
            tag.Name = packet.NewTag;
        });
    }

    private static void RegisterLocks(ReplicationContext ctx)
    {
        ctx.RegisterRelayed<LockPacket>((packet, peer) => SetLockOwner(ctx, packet.NetworkId, packet.UserId));
        ctx.RegisterRelayed<UnlockPacket>((packet, peer) => SetLockOwner(ctx, packet.NetworkId, -1));
    }

    private static void SetLockOwner(ReplicationContext ctx, int networkId, int userId)
    {
        if (!ctx.TryGetEntity(networkId, out var entity)) return;
        if (!ctx.Registry.HasComponentByType(entity, typeof(NetworkIdentityComponent))) return;

        ref var identity = ref ctx.Registry.GetComponent<NetworkIdentityComponent>(entity);
        identity.LockUserId = userId;
    }
}
