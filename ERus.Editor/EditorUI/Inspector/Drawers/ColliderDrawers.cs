using ImGuiNET;
using Silk.NET.Maths;
using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Inspector.Drawers;

/// <summary>
/// Linhas comuns a todos os colliders. Padrao valor/out porque os componentes
/// expoem propriedades — nao da para passar <c>ref</c> para elas.
/// </summary>
internal static class ColliderFields
{
    public static bool Radius(string id, float value, out float result)
    {
        result = value;
        return ImGui.DragFloat(id, ref result, 0.1f);
    }

    public static bool Height(string id, float value, out float result)
    {
        result = value;
        return ImGui.DragFloat(id, ref result, 0.1f);
    }

    public static bool Center(string id, Vector3D<float> value, out Vector3D<float> result)
    {
        InspectorContext.PropertyLabel("Center");
        bool changed = InspectorContext.DragVector3(id, value, out result, 0.1f);
        ImGui.PopItemWidth();
        return changed;
    }

    public static bool IsTrigger(string id, bool value, out bool result)
    {
        InspectorContext.PropertyLabel("Is Trigger");
        result = value;
        bool changed = ImGui.Checkbox(id, ref result);
        ImGui.PopItemWidth();
        return changed;
    }
}

public sealed class BoxColliderDrawer : ComponentDrawer<BoxColliderComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref BoxColliderComponent coll)
    {
        if (!ImGui.CollapsingHeader("Box Collider", ImGuiTreeNodeFlags.DefaultOpen)) return false;

        if (InspectorContext.BeginPropertyTable("BoxColliderTable"))
        {
            InspectorContext.PropertyLabel("Size");
            if (InspectorContext.DragVector3("##BoxSize", coll.Size, out var size, 0.1f)) coll.Size = size;
            ImGui.PopItemWidth();

            if (ColliderFields.Center("##BoxCenter", coll.Center, out var center)) coll.Center = center;
            if (ColliderFields.IsTrigger("##BoxIsTrigger", coll.IsTrigger, out var trigger)) coll.IsTrigger = trigger;
            ImGui.EndTable();
        }

        return InspectorContext.RemoveComponentButton("Remove Box Collider");
    }
}

public sealed class SphereColliderDrawer : ComponentDrawer<SphereColliderComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref SphereColliderComponent coll)
    {
        if (!ImGui.CollapsingHeader("Sphere Collider", ImGuiTreeNodeFlags.DefaultOpen)) return false;

        if (InspectorContext.BeginPropertyTable("SphereColliderTable"))
        {
            InspectorContext.PropertyLabel("Radius");
            if (ColliderFields.Radius("##SphereRadius", coll.Radius, out var radius)) coll.Radius = radius;
            ImGui.PopItemWidth();

            if (ColliderFields.Center("##SphereCenter", coll.Center, out var center)) coll.Center = center;
            if (ColliderFields.IsTrigger("##SphereIsTrigger", coll.IsTrigger, out var trigger)) coll.IsTrigger = trigger;
            ImGui.EndTable();
        }

        return InspectorContext.RemoveComponentButton("Remove Sphere Collider");
    }
}

public sealed class CapsuleColliderDrawer : ComponentDrawer<CapsuleColliderComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref CapsuleColliderComponent coll)
    {
        if (!ImGui.CollapsingHeader("Capsule Collider", ImGuiTreeNodeFlags.DefaultOpen)) return false;

        if (InspectorContext.BeginPropertyTable("CapsuleColliderTable"))
        {
            InspectorContext.PropertyLabel("Radius");
            if (ColliderFields.Radius("##CapsuleRadius", coll.Radius, out var radius)) coll.Radius = radius;
            ImGui.PopItemWidth();

            InspectorContext.PropertyLabel("Height");
            if (ColliderFields.Height("##CapsuleHeight", coll.Height, out var height)) coll.Height = height;
            ImGui.PopItemWidth();

            if (ColliderFields.Center("##CapsuleCenter", coll.Center, out var center)) coll.Center = center;
            if (ColliderFields.IsTrigger("##CapsuleIsTrigger", coll.IsTrigger, out var trigger)) coll.IsTrigger = trigger;
            ImGui.EndTable();
        }

        return InspectorContext.RemoveComponentButton("Remove Capsule Collider");
    }
}

public sealed class CylinderColliderDrawer : ComponentDrawer<CylinderColliderComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref CylinderColliderComponent coll)
    {
        if (!ImGui.CollapsingHeader("Cylinder Collider", ImGuiTreeNodeFlags.DefaultOpen)) return false;

        if (InspectorContext.BeginPropertyTable("CylinderColliderTable"))
        {
            InspectorContext.PropertyLabel("Radius");
            if (ColliderFields.Radius("##CylinderRadius", coll.Radius, out var radius)) coll.Radius = radius;
            ImGui.PopItemWidth();

            InspectorContext.PropertyLabel("Height");
            if (ColliderFields.Height("##CylinderHeight", coll.Height, out var height)) coll.Height = height;
            ImGui.PopItemWidth();

            if (ColliderFields.Center("##CylinderCenter", coll.Center, out var center)) coll.Center = center;
            if (ColliderFields.IsTrigger("##CylinderIsTrigger", coll.IsTrigger, out var trigger)) coll.IsTrigger = trigger;
            ImGui.EndTable();
        }

        return InspectorContext.RemoveComponentButton("Remove Cylinder Collider");
    }
}

public sealed class MeshColliderDrawer : ComponentDrawer<MeshColliderComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref MeshColliderComponent coll)
    {
        if (!ImGui.CollapsingHeader("Mesh Collider", ImGuiTreeNodeFlags.DefaultOpen)) return false;

        if (InspectorContext.BeginPropertyTable("MeshColliderTable"))
        {
            InspectorContext.PropertyLabel("Is Convex");
            bool isConvex = coll.IsConvex;
            if (ImGui.Checkbox("##MeshIsConvex", ref isConvex)) coll.IsConvex = isConvex;
            ImGui.PopItemWidth();

            if (ColliderFields.Center("##MeshCenter", coll.Center, out var center)) coll.Center = center;
            if (ColliderFields.IsTrigger("##MeshIsTrigger", coll.IsTrigger, out var trigger)) coll.IsTrigger = trigger;

            InspectorContext.PropertyLabel("Asset Path (.obj/.gltf)");
            string path = ctx.GetAssetPath(coll.AssetGuid);
            ImGui.BeginDisabled();
            ImGui.InputText("##MeshCollPath", ref path, 512);
            ImGui.EndDisabled();
            if (InspectorContext.TryAcceptAssetDrop(AssetExtensions.Models, out string dropped))
            {
                var guid = ctx.ResolveAssetGuid(dropped);
                if (guid.HasValue) coll.AssetGuid = guid.Value;
            }
            ImGui.PopItemWidth();

            ImGui.EndTable();
        }

        return InspectorContext.RemoveComponentButton("Remove Mesh Collider");
    }
}
