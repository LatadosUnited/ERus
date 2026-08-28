using System;
using System.Collections.Generic;
using ERus.Engine.ECS;
using ERus.Engine.Network.Packets.Events;

namespace ERus.Engine.Network.Replication.Handlers;

/// <summary>
/// Sincroniza a edição de componentes feita por outro colaborador:
/// mesh, material, câmera, física e a lista de scripts anexados.
/// </summary>
public sealed class ComponentSyncHandler : IReplicationHandler
{
    public void Register(ReplicationContext ctx)
    {
        RegisterMesh(ctx);
        RegisterMaterial(ctx);
        RegisterCamera(ctx);
        RegisterPhysics(ctx);
        RegisterScripts(ctx);
    }

    private static void RegisterMesh(ReplicationContext ctx)
    {
        ctx.RegisterRelayed<UpdateMeshPacket>((packet, peer) =>
        {
            if (!ctx.TryGetEntity(packet.NetworkId, out var entity)) return;

            var mesh = Current(ctx, entity, () => new MeshComponent());
            mesh.AssetHash = packet.AssetHash;

            if (string.IsNullOrEmpty(packet.AssetHash))
            {
                // Voltou para uma primitiva: descarta a referência de asset.
                mesh.Type = (PrimitiveMeshType)packet.MeshType;
                mesh.AssetGuid = Guid.Empty;
            }
            else
            {
                var guid = ctx.ResolveGuidByAssetHash(packet.AssetHash);
                mesh.AssetGuid = guid ?? Guid.Empty;
                mesh.Type = guid.HasValue ? PrimitiveMeshType.None : PrimitiveMeshType.Cube; // Placeholder
            }

            ctx.SetOrAdd(entity, mesh);
        });
    }

    private static void RegisterMaterial(ReplicationContext ctx)
    {
        ctx.RegisterRelayed<UpdateMaterialPacket>((packet, peer) =>
        {
            if (!ctx.TryGetEntity(packet.NetworkId, out var entity)) return;

            var mat = Current(ctx, entity, () => new MaterialComponent());
            mat.ColorTint = packet.ColorTint;
            mat.Tiling = packet.Tiling;
            mat.Offset = packet.Offset;
            mat.Metallic = packet.Metallic;
            mat.Roughness = packet.Roughness;
            mat.IsTransparent = packet.IsTransparent;
            mat.AlphaCutoff = packet.AlphaCutoff;
            mat.AlbedoTextureHash = packet.TextureHash;

            if (string.IsNullOrEmpty(packet.TextureHash))
            {
                mat.AlbedoTextureGuid = Guid.Empty;
            }
            else
            {
                // Se o download ainda não chegou, mantém o GUID atual e aguarda o swap.
                var guid = ctx.ResolveGuidByAssetHash(packet.TextureHash);
                if (guid.HasValue) mat.AlbedoTextureGuid = guid.Value;
            }

            ctx.SetOrAdd(entity, mat);
        });
    }

    private static void RegisterCamera(ReplicationContext ctx)
    {
        ctx.RegisterRelayed<UpdateCameraPacket>((packet, peer) =>
        {
            if (!ctx.TryGetEntity(packet.NetworkId, out var entity)) return;

            var cam = Current(ctx, entity, () => new CameraComponent());
            cam.FieldOfView = packet.FieldOfView;
            cam.IsPrimary = packet.IsPrimary;
            cam.NearClip = packet.NearClip;
            cam.FarClip = packet.FarClip;

            ctx.SetOrAdd(entity, cam);
        });
    }

    private static void RegisterPhysics(ReplicationContext ctx)
    {
        ctx.RegisterRelayed<UpdatePhysicsPacket>((packet, peer) =>
        {
            if (!ctx.TryGetEntity(packet.NetworkId, out var entity)) return;

            var rb = Current(ctx, entity, () => new RigidBodyComponent());
            rb.Mass = packet.Mass;
            rb.LinearDrag = packet.LinearDrag;
            rb.AngularDrag = packet.AngularDrag;
            rb.UseGravity = packet.UseGravity;
            rb.IsKinematic = packet.IsKinematic;
            rb.Constraints = (RigidbodyConstraints)packet.Constraints;

            ctx.SetOrAdd(entity, rb);
        });
    }

    private static void RegisterScripts(ReplicationContext ctx)
    {
        ctx.RegisterRelayed<UpdateScriptPacket>((packet, peer) =>
        {
            if (!ctx.TryGetEntity(packet.NetworkId, out var entity)) return;

            var scriptComp = Current(ctx, entity, () => new ScriptComponent());
            scriptComp.Scripts.Clear();

            foreach (var script in packet.Scripts)
            {
                scriptComp.Scripts.Add(new ScriptData
                {
                    ScriptTypeName = script.ScriptTypeName,
                    FieldValues = new Dictionary<string, string>(script.FieldValues)
                });
            }

            ctx.SetOrAdd(entity, scriptComp);
        });
    }

    /// <summary>
    /// Cópia do componente atual da entidade, ou uma instância nova se ela ainda não o tiver —
    /// atualizações parciais preservam os campos que o pacote não carrega.
    /// A factory é explícita para garantir que o construtor sem parâmetros do struct
    /// (que carrega os valores default do componente) seja de fato chamado.
    /// </summary>
    private static T Current<T>(ReplicationContext ctx, Entity entity, Func<T> create) where T : struct, IComponent
        => ctx.Registry.HasComponentByType(entity, typeof(T))
            ? ctx.Registry.GetComponent<T>(entity)
            : create();
}
