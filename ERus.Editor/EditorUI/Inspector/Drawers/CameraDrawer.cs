using ImGuiNET;
using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Inspector.Drawers;

public sealed class CameraDrawer : ComponentDrawer<CameraComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref CameraComponent cam)
    {
        if (!ImGui.CollapsingHeader("Camera", ImGuiTreeNodeFlags.DefaultOpen)) return false;
        if (!InspectorContext.BeginPropertyTable("CameraTable")) return false;

        InspectorContext.PropertyLabel("Primary");
        bool isPrimary = cam.IsPrimary;
        if (ImGui.Checkbox("##Primary", ref isPrimary)) cam.IsPrimary = isPrimary;
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Field of View");
        float fov = cam.FieldOfView;
        if (ImGui.SliderFloat("##FOV", ref fov, 10f, 120f)) cam.FieldOfView = fov;
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Near Clip");
        float near = cam.NearClip;
        if (ImGui.DragFloat("##Near", ref near, 0.01f, 0.01f, 100f)) cam.NearClip = near;
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Far Clip");
        float far = cam.FarClip;
        if (ImGui.DragFloat("##Far", ref far, 1f, 10f, 10000f)) cam.FarClip = far;
        ImGui.PopItemWidth();

        ImGui.EndTable();
        return false;
    }
}
