using ImGuiNET;
using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Inspector.Drawers;

public sealed class TransformDrawer : ComponentDrawer<TransformComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref TransformComponent transform)
    {
        if (!ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen)) return false;
        if (!InspectorContext.BeginPropertyTable("TransformTable")) return false;

        InspectorContext.PropertyLabel("Position");
        if (InspectorContext.DragVector3("##Pos", transform.Position, out var position, 0.1f))
            transform.Position = position;
        ctx.TrackUndo("Position");
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Rotation");
        if (InspectorContext.DragVector3("##Rot", transform.Rotation, out var rotation, 1.0f))
            transform.Rotation = rotation;
        ctx.TrackUndo("Rotation");
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Scale");
        if (InspectorContext.DragVector3("##Sca", transform.Scale, out var scale, 0.1f))
            transform.Scale = scale;
        ctx.TrackUndo("Scale");
        ImGui.PopItemWidth();

        ImGui.EndTable();
        return false;
    }
}
