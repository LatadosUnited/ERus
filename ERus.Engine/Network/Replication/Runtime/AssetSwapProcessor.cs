using System;
using System.Collections.Concurrent;
using ERus.Engine.Core;
using ERus.Engine.ECS;
using ERus.Engine.Network.Core;
using ERus.Engine.Scripting;

namespace ERus.Engine.Network.Replication.Runtime;

/// <summary>
/// Quando um asset referenciado por hash termina de baixar, troca o placeholder pelo
/// asset real nas entidades que o aguardavam.
/// O download chega em thread de rede; a fila garante que a escrita no ECS aconteça
/// no Update, em thread única.
/// </summary>
public sealed class AssetSwapProcessor : IDisposable
{
    private readonly ReplicationContext _ctx;
    private readonly AssetSyncManager? _assetSync;
    private readonly ConcurrentQueue<(string Hash, string Path)> _completedDownloads = new();

    public AssetSwapProcessor(ReplicationContext ctx)
    {
        _ctx = ctx;
        _assetSync = ctx.AssetSync;

        if (_assetSync != null)
            _assetSync.OnAssetDownloaded += Enqueue;
    }

    private void Enqueue(string hash, string path)
    {
        _completedDownloads.Enqueue((hash, path));
        ConsoleLog.Log($"[Rede] Asset baixado e enfileirado para swap de malha: {hash}");
    }

    /// <summary>Aplica os downloads concluídos desde o último frame.</summary>
    public void Process()
    {
        while (_completedDownloads.TryDequeue(out var downloaded))
        {
            var guid = _ctx.Engine.AssetDatabase.GetGuidByPath(downloaded.Path);
            if (!guid.HasValue) continue;

            foreach (var entity in _ctx.Registry.GetLivingEntities())
                SwapEntityAssets(entity, downloaded.Hash, guid.Value);
        }
    }

    private void SwapEntityAssets(Entity entity, string hash, Guid guid)
    {
        var registry = _ctx.Registry;

        if (registry.HasComponentByType(entity, typeof(MeshComponent)))
        {
            ref var mesh = ref registry.GetComponent<MeshComponent>(entity);
            if (mesh.AssetHash == hash)
            {
                mesh.AssetGuid = guid;
                mesh.Type = PrimitiveMeshType.None; // Remove o placeholder
            }
        }

        if (registry.HasComponentByType(entity, typeof(MaterialComponent)))
        {
            ref var mat = ref registry.GetComponent<MaterialComponent>(entity);
            if (mat.AlbedoTextureHash == hash) mat.AlbedoTextureGuid = guid;
        }

        if (registry.HasComponentByType(entity, typeof(SpriteRendererComponent)))
        {
            ref var sprite = ref registry.GetComponent<SpriteRendererComponent>(entity);
            if (sprite.SpriteHash == hash) sprite.SpriteGuid = guid;
        }
    }

    public void Dispose()
    {
        if (_assetSync != null)
            _assetSync.OnAssetDownloaded -= Enqueue;
    }
}
