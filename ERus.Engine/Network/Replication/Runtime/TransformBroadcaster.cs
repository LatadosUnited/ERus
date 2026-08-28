using System;
using ERus.Engine.ECS;

namespace ERus.Engine.Network.Replication.Runtime;

/// <summary>
/// No host, transmite os transforms marcados como sujos a uma taxa fixa,
/// desacoplando a frequência de rede da taxa de quadros.
/// </summary>
public sealed class TransformBroadcaster
{
    private readonly ReplicationContext _ctx;
    private readonly ReplicationSender _sender;
    private double _accumulator;

    /// <summary>Taxa de replicação de pacotes de estado por segundo (Hz). Padrão: 30Hz.</summary>
    public float TickRate { get; set; } = 30f;

    public TransformBroadcaster(ReplicationContext ctx, ReplicationSender sender)
    {
        _ctx = ctx;
        _sender = sender;
    }

    public void Update(double deltaTime)
    {
        if (!ElapsedTick(deltaTime)) return;
        if (!_ctx.IsHost) return;

        foreach (var entity in _ctx.Registry.GetLivingEntities())
        {
            if (!_ctx.Registry.HasComponentByType(entity, typeof(TransformComponent))) continue;
            if (!_ctx.Registry.HasComponentByType(entity, typeof(NetworkIdentityComponent))) continue;

            ref var transform = ref _ctx.Registry.GetComponent<TransformComponent>(entity);
            if (!transform.IsDirty) continue;

            int networkId = _ctx.Registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
            _sender.SendTransform(networkId, transform.Position, transform.Rotation, transform.Scale);
            transform.IsDirty = false;
        }
    }

    /// <summary>Consome o acumulador e informa se um tick de rede venceu neste frame.</summary>
    private bool ElapsedTick(double deltaTime)
    {
        _accumulator += deltaTime;

        double interval = 1.0 / System.Math.Max(1.0, TickRate);
        if (_accumulator < interval) return false;

        _accumulator %= interval;
        return true;
    }
}
