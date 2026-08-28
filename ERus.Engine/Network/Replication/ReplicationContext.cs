using System;
using LiteNetLib;
using ERus.Engine.ECS;
using ERus.Engine.Modules;
using ERus.Engine.Network.Core;

namespace ERus.Engine.Network.Replication;

/// <summary>
/// Dependências e helpers compartilhados por todos os handlers de replicação.
/// Concentra os padrões que antes se repetiam em cada handler inline:
/// relay condicional no host, escrita "atualiza-ou-adiciona" no ECS e resolução
/// de GUID de asset a partir do hash de rede.
/// </summary>
public sealed class ReplicationContext
{
    public Registry Registry { get; }
    public ERus.Engine.Core.Engine Engine { get; }
    public NetworkTransport Transport { get; }
    public NetworkPacketDispatcher Dispatcher { get; }
    public NetworkIdentityMap IdentityMap { get; }
    public EntityTickTracker Ticks { get; }

    public ReplicationContext(
        Registry registry,
        ERus.Engine.Core.Engine engine,
        NetworkTransport transport,
        NetworkPacketDispatcher dispatcher,
        NetworkIdentityMap identityMap,
        EntityTickTracker ticks)
    {
        Registry = registry;
        Engine = engine;
        Transport = transport;
        Dispatcher = dispatcher;
        IdentityMap = identityMap;
        Ticks = ticks;
    }

    // --- Acesso a módulos ---------------------------------------------------

    public bool IsHost => Transport.IsHost;

    public NetworkModule? Network => Engine.GetModule<NetworkModule>();

    public AssetSyncManager? AssetSync => Network?.NetworkManager?.AssetSync;

    public ECSModule? Ecs => Engine.GetModule<ECSModule>();

    public bool TryGetEntity(int networkId, out Entity entity) => IdentityMap.TryGetEntity(networkId, out entity);

    // --- Registro de handlers ----------------------------------------------

    /// <summary>
    /// Registra um handler que, quando executado no host, retransmite o pacote para os
    /// demais peers antes de aplicá-lo localmente. Cobre a maioria dos pacotes de evento.
    /// </summary>
    public void RegisterRelayed<T>(Action<T, NetPeer> apply, DeliveryMethod relayMethod = DeliveryMethod.ReliableOrdered)
        where T : class, new()
    {
        Dispatcher.SubscribeReusable<T>((packet, peer) =>
        {
            if (IsHost) Dispatcher.SendToAllExcept(packet, peer, relayMethod);
            apply(packet, peer);
        });
    }

    /// <summary>
    /// Registra um handler cru, para os pacotes cujo relay é condicional
    /// (ex.: RPC de servidor não é retransmitido) ou precisa ocorrer após a aplicação.
    /// </summary>
    public void RegisterHandler<T>(Action<T, NetPeer> handler) where T : class, new()
        => Dispatcher.SubscribeReusable<T>(handler);

    /// <summary>Retransmite um pacote para todos os peers exceto a origem. Só faz sentido no host.</summary>
    public void RelayToOthers<T>(T packet, NetPeer? origin, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        where T : class, new()
        => Dispatcher.SendToAllExcept(packet, origin, method);

    // --- Helpers de ECS -----------------------------------------------------

    /// <summary>
    /// Escreve o componente na entidade, sobrescrevendo se já existir.
    /// Substitui o par "if (Has) Get() = x; else Add(x);" repetido em cada handler.
    /// </summary>
    public void SetOrAdd<T>(Entity entity, T component) where T : struct, IComponent
    {
        if (Registry.HasComponentByType(entity, typeof(T)))
            Registry.GetComponent<T>(entity) = component;
        else
            Registry.AddComponent(entity, component);
    }

    /// <summary>
    /// Traduz o hash de asset recebido pela rede no GUID local correspondente.
    /// Retorna null se o asset ainda não foi baixado ou não está no AssetDatabase —
    /// nesse caso o chamador aplica um placeholder e aguarda o
    /// <see cref="Runtime.AssetSwapProcessor"/> corrigir quando o download terminar.
    /// </summary>
    public Guid? ResolveGuidByAssetHash(string? assetHash)
    {
        if (string.IsNullOrEmpty(assetHash)) return null;

        string? localPath = AssetSync?.GetFilePathByHash(assetHash);
        if (string.IsNullOrEmpty(localPath)) return null;

        return Engine.AssetDatabase.GetGuidByPath(localPath);
    }
}
