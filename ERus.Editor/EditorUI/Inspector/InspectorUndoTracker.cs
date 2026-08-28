using ERus.Engine.ECS;
using ImGuiNET;
using ERus.Editor.EditorUI.Commands;

namespace ERus.Editor.EditorUI.Inspector;

/// <summary>
/// Captura o estado da entidade quando um widget entra em edicao e registra um
/// <see cref="EntityEditCommand"/> quando a edicao termina.
/// Mantem estado entre frames, portanto vive no <see cref="Panels.InspectorWindow"/>.
/// </summary>
public sealed class InspectorUndoTracker
{
    private readonly UndoSystem _undoSystem;

    private string? _beforeJson;
    private bool _isEditing;

    public InspectorUndoTracker(UndoSystem undoSystem)
    {
        _undoSystem = undoSystem;
    }

    /// <summary>
    /// Deve ser chamado logo apos o widget cujo valor esta sendo editado.
    /// </summary>
    public void Track(string propertyName, Registry registry, Entity entity)
    {
        if (ImGui.IsItemActivated() && !_isEditing)
        {
            _beforeJson = SceneSerializer.SerializeEntityToJson(entity, registry);
            _isEditing = true;
        }

        if (ImGui.IsItemDeactivatedAfterEdit() && _isEditing && _beforeJson != null)
        {
            string afterJson = SceneSerializer.SerializeEntityToJson(entity, registry);
            _undoSystem.Record(new EntityEditCommand(entity, registry, _beforeJson, afterJson, propertyName));
            _isEditing = false;
            _beforeJson = null;
        }
    }
}
