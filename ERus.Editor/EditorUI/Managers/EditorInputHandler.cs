using System.Linq;
using ImGuiNET;
using ERus.Engine.Core;

namespace ERus.Editor.EditorUI.Managers;

public class EditorInputHandler
{
    private ERus.Engine.Core.Engine _engine;
    private UndoSystem _undoSystem;

    public EditorInputHandler(ERus.Engine.Core.Engine engine, UndoSystem undoSystem)
    {
        _engine = engine;
        _undoSystem = undoSystem;
    }

    public void HandleShortcuts(ImGuiIOPtr io)
    {
        if (io.WantTextInput) return;

        bool ctrl = io.KeyCtrl;
        bool shift = io.KeyShift;

        // Undo / Redo
        if (ctrl && !shift && ImGui.IsKeyPressed(ImGuiKey.Z))
            _undoSystem.Undo();
        else if (ctrl && shift && ImGui.IsKeyPressed(ImGuiKey.Z))
            _undoSystem.Redo();

        // Delete Entidades
        if (ImGui.IsKeyPressed(ImGuiKey.Delete) && EditorServices.Selection.SelectedEntity != null)
        {
            var ecs = _engine.GetModule<ERus.Engine.Modules.ECSModule>();
            var netModule = _engine.GetModule<ERus.Engine.Modules.NetworkModule>();

            foreach (var entity in EditorServices.Selection.SelectedEntities.ToList())
            {
                if (netModule != null && ecs.ActiveScene.Registry.HasComponent<ERus.Engine.ECS.NetworkIdentityComponent>(entity))
                {
                    var netId = ecs.ActiveScene.Registry.GetComponent<ERus.Engine.ECS.NetworkIdentityComponent>(entity).NetworkId;
                    netModule.Replication?.SendDestroy(netId);
                }

                ecs.ActiveScene.Registry.DestroyEntity(entity);
            }

            EditorServices.Selection.ClearSelection();
        }
    }
}
