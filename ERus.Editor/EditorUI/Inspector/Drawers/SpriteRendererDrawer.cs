using ImGuiNET;
using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Inspector.Drawers;

public sealed class SpriteRendererDrawer : ComponentDrawer<SpriteRendererComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref SpriteRendererComponent sprite)
    {
        if (!ImGui.CollapsingHeader("Sprite Renderer", ImGuiTreeNodeFlags.DefaultOpen)) return false;
        if (!InspectorContext.BeginPropertyTable("SpriteTable", 110.0f)) return false;

        InspectorContext.PropertyLabel("Color (Tint)");
        var color = sprite.Color;
        if (ImGui.ColorEdit4("##SpriteColor", ref color)) sprite.Color = color;
        ctx.TrackUndo("Sprite Color");
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Sprite (Texture)");
        if (TextureSlot.Draw(ctx, "SpriteTex", sprite.SpriteGuid, out var newSpriteGuid, out _) != TextureSlotResult.Unchanged)
        {
            sprite.SpriteGuid = newSpriteGuid;
            sprite.SpriteHash = null;
        }
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Flip X");
        bool flipX = sprite.FlipX;
        if (ImGui.Checkbox("##FlipX", ref flipX)) sprite.FlipX = flipX;
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Flip Y");
        bool flipY = sprite.FlipY;
        if (ImGui.Checkbox("##FlipY", ref flipY)) sprite.FlipY = flipY;
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Sorting Order");
        int order = sprite.SortingOrder;
        if (ImGui.DragInt("##SortingOrder", ref order)) sprite.SortingOrder = order;
        ImGui.PopItemWidth();

        ImGui.EndTable();

        return InspectorContext.RemoveComponentButton($"{FontAwesome.Trash} Remover Sprite Renderer");
    }
}
