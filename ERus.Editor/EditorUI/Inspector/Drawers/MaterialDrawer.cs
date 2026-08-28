using ImGuiNET;
using ERus.Engine.ECS;
using ERus.Engine.Modules;

namespace ERus.Editor.EditorUI.Inspector.Drawers;

public sealed class MaterialDrawer : ComponentDrawer<MaterialComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref MaterialComponent mat)
    {
        if (!ImGui.CollapsingHeader("Material", ImGuiTreeNodeFlags.DefaultOpen)) return false;
        if (!InspectorContext.BeginPropertyTable("MaterialTable", 110.0f)) return false;

        bool edited = false;

        InspectorContext.PropertyLabel("Color Tint");
        var tint = mat.ColorTint;
        if (ImGui.ColorEdit4("##ColorTint", ref tint)) { mat.ColorTint = tint; edited = true; }
        ctx.TrackUndo("Color Tint");
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Albedo (Texture)");
        var slotResult = TextureSlot.Draw(ctx, "Tex", mat.AlbedoTextureGuid, out var newTexGuid, out string dropped);
        if (slotResult != TextureSlotResult.Unchanged)
        {
            mat.AlbedoTextureGuid = newTexGuid;
            mat.AlbedoTextureHash = null;
            edited = true;
            if (slotResult == TextureSlotResult.Assigned)
                AnnounceAlbedoTexture(ctx, dropped);
        }
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Tiling");
        var tiling = mat.Tiling;
        if (ImGui.DragFloat2("##Tiling", ref tiling, 0.05f)) { mat.Tiling = tiling; edited = true; }
        ctx.TrackUndo("Tiling");
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Offset");
        var offset = mat.Offset;
        if (ImGui.DragFloat2("##Offset", ref offset, 0.05f)) { mat.Offset = offset; edited = true; }
        ctx.TrackUndo("Offset");
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Metallic");
        float metallic = mat.Metallic;
        if (ImGui.SliderFloat("##Metallic", ref metallic, 0.0f, 1.0f)) { mat.Metallic = metallic; edited = true; }
        ctx.TrackUndo("Metallic");
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Roughness");
        float roughness = mat.Roughness;
        if (ImGui.SliderFloat("##Roughness", ref roughness, 0.0f, 1.0f)) { mat.Roughness = roughness; edited = true; }
        ctx.TrackUndo("Roughness");
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Is Transparent");
        bool isTransparent = mat.IsTransparent;
        if (ImGui.Checkbox("##IsTransparent", ref isTransparent)) { mat.IsTransparent = isTransparent; edited = true; }
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Alpha Cutoff");
        float cutoff = mat.AlphaCutoff;
        if (ImGui.SliderFloat("##AlphaCutoff", ref cutoff, 0.0f, 1.0f)) { mat.AlphaCutoff = cutoff; edited = true; }
        ctx.TrackUndo("Alpha Cutoff");
        ImGui.PopItemWidth();

        ImGui.EndTable();

        if (edited && ctx.TryGetNetworkId(out int netId))
        {
            ctx.Replication?.SendUpdateMaterial(netId, mat.ColorTint, mat.AlbedoTextureHash, mat.Tiling,
                mat.Offset, mat.Metallic, mat.Roughness, mat.IsTransparent, mat.AlphaCutoff);
        }

        return InspectorContext.RemoveComponentButton($"{FontAwesome.Trash} Remover Material");
    }

    /// <summary>
    /// Publica a textura na rede; quando o hash volta, reaplica no componente e replica
    /// o material completo (a entidade pode ter sumido nesse meio tempo).
    /// </summary>
    private static void AnnounceAlbedoTexture(InspectorContext ctx, string assetPath)
    {
        var engine = ctx.Engine;
        var registry = ctx.Registry;
        var target = ctx.Entity;

        ctx.AnnounceAsset(assetPath, hash =>
        {
            if (!registry.IsAlive(target)) return;
            if (!registry.HasComponentByType(target, typeof(MaterialComponent))) return;

            ref var updatedMat = ref registry.GetComponent<MaterialComponent>(target);
            updatedMat.AlbedoTextureHash = hash;

            if (!registry.HasComponentByType(target, typeof(NetworkIdentityComponent))) return;
            int netId = registry.GetComponent<NetworkIdentityComponent>(target).NetworkId;
            engine.GetModule<NetworkModule>()?.Replication?.SendUpdateMaterial(netId, updatedMat.ColorTint, hash,
                updatedMat.Tiling, updatedMat.Offset, updatedMat.Metallic, updatedMat.Roughness,
                updatedMat.IsTransparent, updatedMat.AlphaCutoff);
        });
    }
}
