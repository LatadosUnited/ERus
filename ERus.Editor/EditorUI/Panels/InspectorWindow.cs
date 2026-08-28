using System;
using System.Collections.Concurrent;
using ImGuiNET;
using System.Numerics;
using ERus.Engine.Modules;
using ERus.Engine.ECS;
using ERus.Editor.EditorUI.Inspector;

namespace ERus.Editor.EditorUI.Panels;

/// <summary>
/// Painel de inspecao da selecao atual. Nao conhece os componentes do ECS:
/// apenas monta o <see cref="InspectorContext"/> e delega aos drawers registrados
/// em <see cref="ComponentDrawerRegistry"/>.
/// </summary>
public class InspectorWindow : EditorWindow
{
    private readonly ERus.Engine.Core.Engine _engine;
    private readonly InspectorUndoTracker _undoTracker;

    /// <summary>Acoes agendadas por callbacks assincronos (ex.: hash de asset) para rodar na main thread.</summary>
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();

    public InspectorWindow(EditorUIController controller, ERus.Engine.Core.Engine engine) : base("Inspector")
    {
        _engine = engine;
        _undoTracker = new InspectorUndoTracker(controller.UndoSystem);
    }

    protected override void DrawContent()
    {
        while (_mainThreadActions.TryDequeue(out var action)) action();

        if (EditorServices.Selection.SelectedEntities.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "Nenhuma Entidade Selecionada");
            return;
        }

        var ecsModule = _engine.GetModule<ECSModule>();
        if (ecsModule == null) return;

        var registry = ecsModule.ActiveScene.Registry;

        if (EditorServices.Selection.SelectedEntities.Count > 1)
        {
            BatchInspector.Draw(_engine, registry);
            return;
        }

        DrawSingleEntity(registry, EditorServices.Selection.SelectedEntity!.Value);
    }

    private void DrawSingleEntity(Registry registry, Entity entity)
    {
        var ctx = new InspectorContext(_engine, registry, entity, _undoTracker, _mainThreadActions);

        bool isLocked = ctx.IsLockedByOtherUser();
        if (isLocked)
        {
            ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "🔒 Bloqueado por outro usuário.");
            ImGui.Separator();
            ImGui.BeginDisabled();
        }

        foreach (var drawer in ComponentDrawerRegistry.Drawers)
            drawer.Draw(ctx);

        ImGui.Separator();
        ImGui.Spacing();

        AddComponentMenu.Draw(ctx);

        ImGui.Spacing();

        DrawDestroyButton(ctx);

        // O original abria BeginDisabled sem fechar, vazando o estado para os proximos widgets.
        if (isLocked) ImGui.EndDisabled();
    }

    private static void DrawDestroyButton(InspectorContext ctx)
    {
        if (!InspectorContext.DestructiveButton($"{FontAwesome.Trash} Destroy Entity")) return;

        if (ctx.TryGetNetworkId(out int netId))
            ctx.Replication?.SendDestroy(netId);

        ctx.Registry.DestroyEntity(ctx.Entity);
        EditorServices.Selection.SelectedEntity = null;
    }
}
