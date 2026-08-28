using ERus.Engine.Core;
using ERus.Engine.ECS;
using ERus.Engine.Network.Packets.Events;
using ERus.Engine.Network.Packets.State;
using ERus.Engine.Scripting;

namespace ERus.Engine.Network.Replication.Handlers;

/// <summary>
/// Estado da sessão colaborativa em si — e não das entidades:
/// presença de colaboradores, chat, Play/Edit compartilhado e troca de cena.
/// </summary>
public sealed class SessionReplicationHandler : IReplicationHandler
{
    /// <summary>Cena temporária usada para restaurar o estado de edição ao sair do Play.</summary>
    private const string PlayModeSnapshotPath = "Assets/_temp_play.scene";

    public void Register(ReplicationContext ctx)
    {
        RegisterEngineState(ctx);
        RegisterLoadScene(ctx);
        RegisterPresence(ctx);
    }

    private static void RegisterEngineState(ReplicationContext ctx)
    {
        ctx.RegisterRelayed<EngineStatePacket>((packet, peer) =>
        {
            // O host é a origem da mudança; só os clientes a seguem.
            if (ctx.IsHost) return;

            var targetState = (EngineState)packet.State;
            if (ctx.Engine.State == targetState) return;

            SnapshotOrRestore(ctx, targetState);

            ctx.Engine.State = targetState;
            ConsoleLog.Log($"[Rede] Estado da Engine sincronizado para: {targetState}");
        });
    }

    /// <summary>
    /// Ao entrar em Play salva a cena de edição; ao voltar para Edit restaura o snapshot,
    /// descartando o que a simulação alterou.
    /// </summary>
    private static void SnapshotOrRestore(ReplicationContext ctx, EngineState targetState)
    {
        var ecs = ctx.Ecs;
        if (ecs == null) return;

        string path = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(System.Environment.CurrentDirectory, PlayModeSnapshotPath));

        if (targetState == EngineState.Play && ctx.Engine.State == EngineState.Edit)
            SceneSerializer.SaveScene(path, ecs.ActiveScene);
        else if (targetState == EngineState.Edit && ctx.Engine.State != EngineState.Edit)
            SceneSerializer.LoadScene(path, ecs.ActiveScene);
    }

    private static void RegisterLoadScene(ReplicationContext ctx)
    {
        ctx.RegisterRelayed<LoadScenePacket>((packet, peer) =>
        {
            if (ctx.IsHost) return;

            var ecs = ctx.Ecs;
            if (ecs == null) return;

            ecs.ActiveScene.Clear();
            ctx.IdentityMap.ClearLocalMap();
            ConsoleLog.Log($"[Rede] Host iniciou carregamento da cena {packet.SceneName}. Cena limpa localmente.");
        });
    }

    private static void RegisterPresence(ReplicationContext ctx)
    {
        ctx.RegisterHandler<UserPresencePacket>((packet, peer) =>
        {
            ctx.Network?.NetworkManager?.Presence.UpdatePresence(packet);
            if (ctx.IsHost) ctx.RelayToOthers(packet, peer);
        });

        ctx.RegisterHandler<ChatMessagePacket>((packet, peer) =>
        {
            ctx.Network?.NetworkManager?.Presence.AddChatMessage(packet);
            if (ctx.IsHost) ctx.RelayToOthers(packet, peer);
        });
    }
}
