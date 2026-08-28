using System.Linq;
using ImGuiNET;
using System.Numerics;
using ERus.Engine.ECS;
using ERus.Engine.Modules;

namespace ERus.Editor.EditorUI.Inspector;

/// <summary>
/// Inspector para selecao multipla: aplica deltas de transform e destroi em lote.
/// </summary>
public static class BatchInspector
{
    public static void Draw(ERus.Engine.Core.Engine engine, Registry registry)
    {
        ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f),
            $"{EditorServices.Selection.SelectedEntities.Count} Entidades Selecionadas");
        ImGui.Separator();

        DrawTransformDelta(registry);
        ImGui.Separator();
        DrawDestroyButton(engine, registry);
    }

    private static void DrawTransformDelta(Registry registry)
    {
        if (!ImGui.CollapsingHeader("Transform (Batch)", ImGuiTreeNodeFlags.DefaultOpen)) return;

        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.4f, 1.0f), "Delta aplicado a todas:");

        var deltaPos = Vector3.Zero;
        var deltaRot = Vector3.Zero;
        var deltaScale = Vector3.Zero;
        bool edited = false;

        if (InspectorContext.BeginPropertyTable("BatchTransformTable"))
        {
            InspectorContext.PropertyLabel("Δ Position");
            if (ImGui.DragFloat3("##DeltaPos", ref deltaPos, 0.1f)) edited = true;
            ImGui.PopItemWidth();

            InspectorContext.PropertyLabel("Δ Rotation");
            if (ImGui.DragFloat3("##DeltaRot", ref deltaRot, 0.1f)) edited = true;
            ImGui.PopItemWidth();

            InspectorContext.PropertyLabel("Δ Scale");
            if (ImGui.DragFloat3("##DeltaSca", ref deltaScale, 0.1f)) edited = true;
            ImGui.PopItemWidth();

            ImGui.EndTable();
        }

        if (!edited) return;

        foreach (var entity in EditorServices.Selection.SelectedEntities)
        {
            if (!registry.HasComponentByType(entity, typeof(TransformComponent))) continue;

            ref var t = ref registry.GetComponent<TransformComponent>(entity);
            t.Position = new Silk.NET.Maths.Vector3D<float>(
                t.Position.X + deltaPos.X, t.Position.Y + deltaPos.Y, t.Position.Z + deltaPos.Z);
            t.Rotation = new Silk.NET.Maths.Vector3D<float>(
                t.Rotation.X + deltaRot.X, t.Rotation.Y + deltaRot.Y, t.Rotation.Z + deltaRot.Z);
            t.Scale = new Silk.NET.Maths.Vector3D<float>(
                t.Scale.X + deltaScale.X, t.Scale.Y + deltaScale.Y, t.Scale.Z + deltaScale.Z);
        }
    }

    private static void DrawDestroyButton(ERus.Engine.Core.Engine engine, Registry registry)
    {
        ImGui.Spacing();

        int count = EditorServices.Selection.SelectedEntities.Count;
        if (!InspectorContext.DestructiveButton($"{FontAwesome.Trash} Destroy {count} Entities")) return;

        var replication = engine.GetModule<NetworkModule>()?.Replication;
        foreach (var entity in EditorServices.Selection.SelectedEntities.ToList())
        {
            if (registry.HasComponentByType(entity, typeof(NetworkIdentityComponent)))
            {
                int netId = registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
                replication?.SendDestroy(netId);
            }
            registry.DestroyEntity(entity);
        }
        EditorServices.Selection.ClearSelection();
    }
}
