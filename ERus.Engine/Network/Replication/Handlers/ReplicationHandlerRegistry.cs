using System.Collections.Generic;

namespace ERus.Engine.Network.Replication.Handlers;

/// <summary>
/// Handlers de replicação ativos, por domínio.
/// Para replicar um pacote novo, adicione-o ao handler do domínio correspondente —
/// ou crie um handler novo e registre-o aqui.
/// </summary>
public static class ReplicationHandlerRegistry
{
    public static readonly IReadOnlyList<IReplicationHandler> Handlers = new IReplicationHandler[]
    {
        new TransformReplicationHandler(),
        new EntityLifecycleHandler(),
        new ComponentSyncHandler(),
        new ScriptReplicationHandler(),
        new SessionReplicationHandler(),
    };
}
