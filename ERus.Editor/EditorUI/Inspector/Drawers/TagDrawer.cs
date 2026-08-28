using ImGuiNET;
using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Inspector.Drawers;

/// <summary>Campo de nome no topo do Inspector. Nao usa CollapsingHeader.</summary>
public sealed class TagDrawer : ComponentDrawer<TagComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref TagComponent tag)
    {
        string name = tag.Name ?? "Entity";

        ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[0]);
        ImGui.PushItemWidth(-1);

        if (ImGui.InputText("##Name", ref name, 128))
        {
            tag.Name = name;
            if (ctx.TryGetNetworkId(out int netId))
                ctx.Replication?.SendRename(netId, name);
        }
        ctx.TrackUndo("Tag Name");

        ImGui.PopItemWidth();
        ImGui.PopFont();
        ImGui.Separator();

        return false;
    }
}
