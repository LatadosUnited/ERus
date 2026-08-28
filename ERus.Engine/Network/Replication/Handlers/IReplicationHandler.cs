namespace ERus.Engine.Network.Replication.Handlers;

/// <summary>
/// Registra os handlers de um domínio de replicação no dispatcher.
/// Para replicar um pacote novo, escreva o handler do domínio correspondente e
/// registre-o em <see cref="ReplicationHandlerRegistry"/> — o
/// <see cref="EntityReplicationSystem"/> não precisa saber que ele existe.
/// </summary>
public interface IReplicationHandler
{
    void Register(ReplicationContext ctx);
}
