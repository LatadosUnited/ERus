using System;
using Silk.NET.Maths;
using ERus.Engine.ECS;

namespace ERus.Engine.Network.Replication.Runtime;

/// <summary>
/// Suaviza a chegada dos transforms replicados: em vez de aplicar cada pacote direto,
/// converge o transform local para o alvo recebido, eliminando o jitter do canal não confiável.
/// </summary>
public sealed class TransformInterpolator
{
    /// <summary>Velocidade de convergência usada quando o componente não define a sua.</summary>
    private const float DefaultLerpSpeed = 18f;

    /// <summary>Distância acima da qual não se interpola — teleporta, para não "voar" até o alvo.</summary>
    private const float TeleportDistance = 15.0f;

    /// <summary>Abaixo disso o alvo é considerado alcançado e a interpolação encerra.</summary>
    private const float SnapEpsilon = 0.005f;

    private readonly ReplicationContext _ctx;

    public TransformInterpolator(ReplicationContext ctx)
    {
        _ctx = ctx;
    }

    public void Update(double deltaTime)
    {
        foreach (var entity in _ctx.Registry.GetLivingEntities())
        {
            if (!_ctx.Registry.HasComponentByType(entity, typeof(TransformComponent))) continue;
            if (!_ctx.Registry.HasComponentByType(entity, typeof(NetworkInterpolationComponent))) continue;

            ref var transform = ref _ctx.Registry.GetComponent<TransformComponent>(entity);
            ref var interp = ref _ctx.Registry.GetComponent<NetworkInterpolationComponent>(entity);

            float lerpSpeed = interp.InterpolationSpeed > 0 ? interp.InterpolationSpeed : DefaultLerpSpeed;
            float step = MathF.Min(1.0f, (float)deltaTime * lerpSpeed);
            bool changed = false;

            if (interp.HasTargetPosition)
            {
                changed = true;
                transform.Position = StepPosition(transform.Position, interp.TargetPosition, step, out bool settled);
                if (settled) interp.HasTargetPosition = false;
            }

            if (interp.HasTargetRotation)
            {
                changed = true;
                transform.Rotation = Step(transform.Rotation, interp.TargetRotation, step, out bool settled);
                if (settled) interp.HasTargetRotation = false;
            }

            if (interp.HasTargetScale)
            {
                changed = true;
                transform.Scale = Step(transform.Scale, interp.TargetScale, step, out bool settled);
                if (settled) interp.HasTargetScale = false;
            }

            // O movimento veio da rede: não remarcar como sujo, senão o host o retransmitiria de volta.
            if (changed) transform.IsDirty = false;
        }
    }

    /// <summary>
    /// Como <see cref="Step"/>, mas com corte de teleporte: uma discrepância muito grande
    /// (respawn, reconexão) é aplicada de uma vez em vez de percorrida.
    /// </summary>
    private static Vector3D<float> StepPosition(Vector3D<float> current, Vector3D<float> target, float step, out bool settled)
    {
        var delta = target - current;

        if (delta.Length > TeleportDistance)
        {
            settled = true;
            return target;
        }

        return Step(current, target, step, out settled);
    }

    /// <summary>
    /// Aproxima o valor atual do alvo. <paramref name="settled"/> indica que o alvo foi
    /// alcançado e a interpolação daquele eixo pode ser encerrada.
    /// </summary>
    private static Vector3D<float> Step(Vector3D<float> current, Vector3D<float> target, float step, out bool settled)
    {
        var delta = target - current;

        settled = delta.Length < SnapEpsilon;
        return settled ? target : current + delta * step;
    }
}
