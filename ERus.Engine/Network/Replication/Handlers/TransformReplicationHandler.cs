using LiteNetLib;
using Silk.NET.Maths;
using ERus.Engine.ECS;
using ERus.Engine.Network.Packets.State;

namespace ERus.Engine.Network.Replication.Handlers;

/// <summary>
/// Recebe as atualizações de transform. Não usa <c>RegisterRelayed</c> porque o relay
/// precisa acontecer depois do descarte de pacotes fora de ordem e em canal não confiável.
/// </summary>
public sealed class TransformReplicationHandler : IReplicationHandler
{
    // Bits de UpdateFlags do TransformPacket.
    private const byte FlagPosition = 1;
    private const byte FlagRotation = 2;
    private const byte FlagScale = 4;

    public void Register(ReplicationContext ctx)
    {
        ctx.RegisterHandler<TransformPacket>((packet, peer) =>
        {
            if (ctx.Ticks.ShouldDrop(packet.NetworkId, packet.Tick)) return;

            if (ctx.IsHost) ctx.RelayToOthers(packet, peer, DeliveryMethod.Unreliable);

            if (!ctx.TryGetEntity(packet.NetworkId, out var entity)) return;
            if (!ctx.Registry.HasComponentByType(entity, typeof(TransformComponent))) return;

            ApplyToInterpolation(ctx, entity, packet);
        });
    }

    /// <summary>
    /// O transform não é escrito direto: vira alvo de interpolação, consumido pelo
    /// <see cref="Runtime.TransformInterpolator"/> a cada frame.
    /// </summary>
    private static void ApplyToInterpolation(ReplicationContext ctx, Entity entity, TransformPacket packet)
    {
        if (!ctx.Registry.HasComponentByType(entity, typeof(NetworkInterpolationComponent)))
            ctx.Registry.AddComponent(entity, new NetworkInterpolationComponent());

        ref var interp = ref ctx.Registry.GetComponent<NetworkInterpolationComponent>(entity);

        if ((packet.UpdateFlags & FlagPosition) != 0)
        {
            interp.TargetPosition = new Vector3D<float>(packet.Position.X, packet.Position.Y, packet.Position.Z);
            interp.HasTargetPosition = true;
        }
        if ((packet.UpdateFlags & FlagRotation) != 0)
        {
            interp.TargetRotation = new Vector3D<float>(packet.Rotation.X, packet.Rotation.Y, packet.Rotation.Z);
            interp.HasTargetRotation = true;
        }
        if ((packet.UpdateFlags & FlagScale) != 0)
        {
            interp.TargetScale = new Vector3D<float>(packet.Scale.X, packet.Scale.Y, packet.Scale.Z);
            interp.HasTargetScale = true;
        }
    }
}
