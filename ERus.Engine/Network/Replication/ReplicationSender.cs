using System;
using LiteNetLib;
using Silk.NET.Maths;
using ERus.Engine.Network.Core;
using ERus.Engine.Network.Packets.Events;
using ERus.Engine.Network.Packets.State;
using ERus.Engine.Network.Collaboration;

namespace ERus.Engine.Network.Replication;

/// <summary>
/// Lado de saída da replicação: monta os pacotes e escolhe a rota.
/// Toda mensagem segue a mesma regra — o host difunde para os peers, o cliente envia
/// ao host, que retransmite. <see cref="Broadcast{T}"/> concentra essa decisão.
/// </summary>
public sealed class ReplicationSender
{
    private readonly NetworkTransport _transport;
    private readonly NetworkPacketDispatcher _dispatcher;
    private readonly ERus.Engine.Core.Engine _engine;
    private readonly EntityTickTracker _ticks;

    /// <summary>Canal dedicado ao fluxo de transform, separado dos eventos confiáveis.</summary>
    private const byte TransformChannel = 1;

    public ReplicationSender(
        NetworkTransport transport,
        NetworkPacketDispatcher dispatcher,
        ERus.Engine.Core.Engine engine,
        EntityTickTracker ticks)
    {
        _transport = transport;
        _dispatcher = dispatcher;
        _engine = engine;
        _ticks = ticks;
    }

    /// <summary>Host difunde para todos; cliente envia ao host, que retransmite.</summary>
    private void Broadcast<T>(T packet, DeliveryMethod method = DeliveryMethod.ReliableOrdered, byte channel = 0)
        where T : class, new()
    {
        if (_transport.IsHost) _dispatcher.SendToAllExcept(packet, null, method, channel);
        else _dispatcher.SendToServer(packet, method, channel);
    }

    private string MyUsername =>
        _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.NetworkManager?.MyUsername
        ?? ("Dev_" + _transport.MyUserId);

    // --- Transform ----------------------------------------------------------

    public void SendTransform(int networkId, Vector3D<float> position, Vector3D<float> rotation, Vector3D<float> scale, byte updateFlags = 7)
        => Broadcast(BuildTransform(networkId, position, rotation, scale, updateFlags), DeliveryMethod.Sequenced, TransformChannel);

    public void SendTransformToPeer(NetPeer peer, int networkId, Vector3D<float> position, Vector3D<float> rotation, Vector3D<float> scale, byte updateFlags = 7)
        => _dispatcher.SendToPeer(peer, BuildTransform(networkId, position, rotation, scale, updateFlags), DeliveryMethod.ReliableOrdered);

    private TransformPacket BuildTransform(int networkId, Vector3D<float> position, Vector3D<float> rotation, Vector3D<float> scale, byte updateFlags)
        => new TransformPacket
        {
            NetworkId = networkId,
            Position = position,
            Rotation = rotation,
            Scale = scale,
            Tick = _ticks.NextOutgoingTick(),
            UpdateFlags = updateFlags
        };

    // --- Ciclo de vida ------------------------------------------------------

    public void SendSpawn(int networkId, string tag, int meshType, string assetHash = "")
        => Broadcast(BuildSpawn(networkId, tag, meshType, assetHash));

    public void SendSpawnToPeer(NetPeer peer, int networkId, string tag, int meshType, string assetHash = "")
        => _dispatcher.SendToPeer(peer, BuildSpawn(networkId, tag, meshType, assetHash), DeliveryMethod.ReliableOrdered);

    private static SpawnEntityPacket BuildSpawn(int networkId, string tag, int meshType, string assetHash)
        => new SpawnEntityPacket { NetworkId = networkId, Tag = tag, MeshType = meshType, AssetHash = assetHash };

    public void SendRename(int networkId, string newTag)
        => Broadcast(new RenameEntityPacket { NetworkId = networkId, NewTag = newTag });

    public void SendDestroy(int networkId)
    {
        Broadcast(new DestroyEntityPacket { NetworkId = networkId });
        _ticks.Forget(networkId); // Limpeza local
    }

    // --- Locks de edição colaborativa ---------------------------------------

    public void RequestLock(int networkId)
        => Broadcast(new LockPacket { NetworkId = networkId, UserId = _transport.MyUserId });

    public void SendLockToPeer(NetPeer peer, int networkId, int userId)
        => _dispatcher.SendToPeer(peer, new LockPacket { NetworkId = networkId, UserId = userId }, DeliveryMethod.ReliableOrdered);

    public void SendUnlock(int networkId)
        => Broadcast(new UnlockPacket { NetworkId = networkId });

    // --- Componentes --------------------------------------------------------

    public void SendUpdateMesh(int networkId, int meshType, string assetHash = "")
        => Broadcast(new UpdateMeshPacket { NetworkId = networkId, MeshType = meshType, AssetHash = assetHash });

    public void SendUpdateMaterial(int networkId, System.Numerics.Vector4 colorTint, string? textureHash,
        System.Numerics.Vector2 tiling, System.Numerics.Vector2 offset, float metallic, float roughness,
        bool isTransparent, float alphaCutoff)
        => Broadcast(new UpdateMaterialPacket
        {
            NetworkId = networkId,
            ColorTint = colorTint,
            TextureHash = textureHash,
            Tiling = tiling,
            Offset = offset,
            Metallic = metallic,
            Roughness = roughness,
            IsTransparent = isTransparent,
            AlphaCutoff = alphaCutoff
        });

    // --- Sessão -------------------------------------------------------------

    public void SendEngineState(byte state)
        => Broadcast(new EngineStatePacket { State = state });

    public void SendUserPresence(int selectedNetworkId, bool isDisconnecting = false)
    {
        string username = MyUsername;
        var (r, g, b) = CollaboratorPresenceManager.GenerateUserColor(username);

        Broadcast(new UserPresencePacket
        {
            UserId = _transport.MyUserId,
            Username = username,
            SelectedNetworkId = selectedNetworkId,
            ColorR = r,
            ColorG = g,
            ColorB = b,
            IsDisconnecting = isDisconnecting
        });
    }

    public void SendChatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var packet = new ChatMessagePacket
        {
            SenderId = _transport.MyUserId,
            Username = MyUsername,
            Message = message,
            Timestamp = DateTime.Now.ToString("HH:mm:ss")
        };

        // O remetente não recebe o próprio relay: registra localmente antes de enviar.
        _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.NetworkManager?.Presence.AddChatMessage(packet);

        Broadcast(packet);
    }
}
