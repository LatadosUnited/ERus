using System;
using ImGuiNET;
using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Inspector.Drawers;

public sealed class RigidBodyDrawer : ComponentDrawer<RigidBodyComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref RigidBodyComponent rb)
    {
        if (!ImGui.CollapsingHeader("Rigidbody", ImGuiTreeNodeFlags.DefaultOpen)) return false;

        if (InspectorContext.BeginPropertyTable("RigidbodyTable"))
        {
            InspectorContext.PropertyLabel("Is Kinematic");
            bool isKinematic = rb.IsKinematic;
            if (ImGui.Checkbox("##IsKinematic", ref isKinematic)) rb.IsKinematic = isKinematic;
            ImGui.PopItemWidth();

            InspectorContext.PropertyLabel("Use Gravity");
            bool useGravity = rb.UseGravity;
            if (ImGui.Checkbox("##UseGravity", ref useGravity)) rb.UseGravity = useGravity;
            ImGui.PopItemWidth();

            InspectorContext.PropertyLabel("Mass");
            float mass = rb.Mass;
            if (ImGui.DragFloat("##Mass", ref mass, 0.1f, 0.01f, 10000f)) rb.Mass = mass;
            ImGui.PopItemWidth();

            InspectorContext.PropertyLabel("Linear Drag");
            float linearDrag = rb.LinearDrag;
            if (ImGui.DragFloat("##LinearDrag", ref linearDrag, 0.1f, 0.0f, 100f)) rb.LinearDrag = linearDrag;
            ImGui.PopItemWidth();

            InspectorContext.PropertyLabel("Angular Drag");
            float angularDrag = rb.AngularDrag;
            if (ImGui.DragFloat("##AngularDrag", ref angularDrag, 0.1f, 0.0f, 100f)) rb.AngularDrag = angularDrag;
            ImGui.PopItemWidth();

            InspectorContext.PropertyLabel("Constraints");
            DrawConstraints(ref rb);
            ImGui.PopItemWidth();

            ImGui.EndTable();
        }

        return InspectorContext.RemoveComponentButton("Remove Rigidbody");
    }

    /// <summary>Combo com checkboxes por eixo; os valores compostos do enum ficam de fora.</summary>
    private static void DrawConstraints(ref RigidBodyComponent rb)
    {
        if (!ImGui.BeginCombo("##Constraints", rb.Constraints.ToString())) return;

        int constraints = (int)rb.Constraints;
        string[] names = Enum.GetNames(typeof(RigidbodyConstraints));
        int[] values = (int[])Enum.GetValues(typeof(RigidbodyConstraints));

        for (int i = 0; i < names.Length; i++)
        {
            if (values[i] == 0) continue; // "None"
            if (values[i] == (int)RigidbodyConstraints.FreezePosition ||
                values[i] == (int)RigidbodyConstraints.FreezeRotation ||
                values[i] == (int)RigidbodyConstraints.FreezeAll) continue; // nomes compostos

            bool isSelected = (constraints & values[i]) == values[i];
            if (ImGui.Checkbox(names[i], ref isSelected))
            {
                if (isSelected) constraints |= values[i];
                else constraints &= ~values[i];
                rb.Constraints = (RigidbodyConstraints)constraints;
            }
        }

        ImGui.EndCombo();
    }
}
