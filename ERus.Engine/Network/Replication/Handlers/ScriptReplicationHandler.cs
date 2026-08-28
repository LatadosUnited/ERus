using ERus.Engine.ECS;
using ERus.Engine.Network.Packets.Events;

namespace ERus.Engine.Network.Replication.Handlers;

/// <summary>
/// RPCs e SyncVars de gameplay. Não usa <c>RegisterRelayed</c> porque o relay é
/// condicional (um ServerRpc morre no host) e deve ocorrer depois da execução local.
/// </summary>
public sealed class ScriptReplicationHandler : IReplicationHandler
{
    public void Register(ReplicationContext ctx)
    {
        ctx.RegisterHandler<ScriptRpcPacket>((packet, peer) =>
        {
            if (ctx.TryGetEntity(packet.NetworkId, out var entity))
            {
                ScriptExecution(ctx)?.ExecuteRpcOnEntity(entity, packet.ScriptTypeName, packet.MethodName, packet.Arguments);
            }

            // Um ServerRpc é endereçado ao host e para nele; um ClientRpc é propagado adiante.
            if (ctx.IsHost && !packet.IsServerRpc)
                ctx.RelayToOthers(packet, peer);
        });

        ctx.RegisterHandler<ScriptSyncVarPacket>((packet, peer) =>
        {
            if (ctx.TryGetEntity(packet.NetworkId, out var entity))
            {
                ScriptExecution(ctx)?.ApplySyncVarOnEntity(entity, packet.ScriptTypeName, packet.FieldName, packet.Value);
            }

            if (ctx.IsHost) ctx.RelayToOthers(packet, peer);
        });
    }

    private static ScriptExecutionSystem? ScriptExecution(ReplicationContext ctx)
        => ctx.Ecs?.GetSystem<ScriptExecutionSystem>();
}
