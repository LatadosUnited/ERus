using LiteNetLib;
using Silk.NET.Maths;
using ERus.Engine.ECS;
using ERus.Engine.Network.Core;
using ERus.Engine.Network.Replication.Handlers;
using ERus.Engine.Network.Replication.Runtime;

namespace ERus.Engine.Network.Replication;

/// <summary>
/// Orquestra a replicação de entidades. Não conhece pacotes: monta o
/// <see cref="ReplicationContext"/>, registra os handlers de
/// <see cref="ReplicationHandlerRegistry"/> e roda os processadores por frame.
/// Os métodos <c>SendXxx</c> permanecem aqui como fachada estável para o editor,
/// delegando ao <see cref="ReplicationSender"/>.
/// </summary>
public class EntityReplicationSystem : BaseSystem
{
    private readonly ReplicationContext _ctx;
    private readonly ReplicationSender _sender;
    private readonly AssetSwapProcessor _assetSwap;
    private readonly TransformBroadcaster _broadcaster;
    private readonly TransformInterpolator _interpolator;

    /// <summary>Taxa de replicação de pacotes de estado por segundo (Hz). Padrão: 30Hz.</summary>
    public float TickRate
    {
        get => _broadcaster.TickRate;
        set => _broadcaster.TickRate = value;
    }

    public EntityReplicationSystem(
        Registry registry,
        ERus.Engine.Core.Engine engine,
        NetworkTransport transport,
        NetworkPacketDispatcher dispatcher,
        NetworkIdentityMap identityMap) : base(registry)
    {
        var ticks = new EntityTickTracker();

        _ctx = new ReplicationContext(registry, engine, transport, dispatcher, identityMap, ticks);
        _sender = new ReplicationSender(transport, dispatcher, engine, ticks);

        _assetSwap = new AssetSwapProcessor(_ctx);
        _broadcaster = new TransformBroadcaster(_ctx, _sender);
        _interpolator = new TransformInterpolator(_ctx);

        foreach (var handler in ReplicationHandlerRegistry.Handlers)
            handler.Register(_ctx);
    }

    public override void Update(double deltaTime)
    {
        _assetSwap.Process();
        _broadcaster.Update(deltaTime);
        _interpolator.Update(deltaTime);
    }

    // --- Fachada de envio ---------------------------------------------------
    // Mantida para não quebrar as ~30 chamadas do editor (`Replication?.SendXxx`).

    public void SendTransform(int networkId, Vector3D<float> position, Vector3D<float> rotation, Vector3D<float> scale, byte updateFlags = 7)
        => _sender.SendTransform(networkId, position, rotation, scale, updateFlags);

    public void SendTransformToPeer(NetPeer peer, int networkId, Vector3D<float> position, Vector3D<float> rotation, Vector3D<float> scale, byte updateFlags = 7)
        => _sender.SendTransformToPeer(peer, networkId, position, rotation, scale, updateFlags);

    public void SendSpawn(int networkId, string tag, int meshType, string assetHash = "")
        => _sender.SendSpawn(networkId, tag, meshType, assetHash);

    public void SendSpawnToPeer(NetPeer peer, int networkId, string tag, int meshType, string assetHash = "")
        => _sender.SendSpawnToPeer(peer, networkId, tag, meshType, assetHash);

    public void SendUpdateMesh(int networkId, int meshType, string assetHash = "")
        => _sender.SendUpdateMesh(networkId, meshType, assetHash);

    public void SendUpdateMaterial(int networkId, System.Numerics.Vector4 colorTint, string? textureHash,
        System.Numerics.Vector2 tiling, System.Numerics.Vector2 offset, float metallic, float roughness,
        bool isTransparent, float alphaCutoff)
        => _sender.SendUpdateMaterial(networkId, colorTint, textureHash, tiling, offset, metallic, roughness, isTransparent, alphaCutoff);

    public void SendLockToPeer(NetPeer peer, int networkId, int userId) => _sender.SendLockToPeer(peer, networkId, userId);

    public void RequestLock(int networkId) => _sender.RequestLock(networkId);

    public void SendUnlock(int networkId) => _sender.SendUnlock(networkId);

    public void SendRename(int networkId, string newTag) => _sender.SendRename(networkId, newTag);

    public void SendDestroy(int networkId) => _sender.SendDestroy(networkId);

    public void SendEngineState(byte state) => _sender.SendEngineState(state);

    public void SendUserPresence(int selectedNetworkId, bool isDisconnecting = false)
        => _sender.SendUserPresence(selectedNetworkId, isDisconnecting);

    public void SendChatMessage(string message) => _sender.SendChatMessage(message);
}
