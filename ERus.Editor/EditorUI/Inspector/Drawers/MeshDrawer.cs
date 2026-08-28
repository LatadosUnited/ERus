using System;
using ImGuiNET;
using ERus.Engine.ECS;
using ERus.Engine.Modules;

namespace ERus.Editor.EditorUI.Inspector.Drawers;

public sealed class MeshDrawer : ComponentDrawer<MeshComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref MeshComponent mesh)
    {
        if (!ImGui.CollapsingHeader("Mesh Renderer", ImGuiTreeNodeFlags.DefaultOpen)) return false;
        if (!InspectorContext.BeginPropertyTable("MeshTable")) return false;

        InspectorContext.PropertyLabel("Primitive Type");
        int currentType = (int)mesh.Type;
        string[] types = Enum.GetNames(typeof(PrimitiveMeshType));
        if (ImGui.Combo("##PrimitiveType", ref currentType, types, types.Length))
        {
            mesh.Type = (PrimitiveMeshType)currentType;
            if (ctx.TryGetNetworkId(out int netId))
                ctx.Replication?.SendUpdateMesh(netId, currentType, mesh.AssetHash ?? "");
        }
        ImGui.PopItemWidth();

        InspectorContext.PropertyLabel("Asset Path (.obj/.gltf)");
        string path = ctx.GetAssetPath(mesh.AssetGuid);

        ImGui.BeginDisabled();
        ImGui.InputText("##AssetPath", ref path, 512);
        ImGui.EndDisabled();

        if (InspectorContext.TryAcceptAssetDrop(AssetExtensions.Models, out string dropped))
        {
            var guid = ctx.ResolveAssetGuid(dropped);
            if (guid.HasValue) mesh.AssetGuid = guid.Value;
            mesh.AssetHash = null; // Limpa o hash ate calcular o novo

            AnnounceMeshAsset(ctx, dropped);
        }
        ImGui.PopItemWidth();

        ImGui.EndTable();
        return false;
    }

    /// <summary>
    /// Publica o modelo na rede; quando o hash volta, reaplica no componente (a entidade
    /// pode ter sido destruida ou perdido o MeshComponent nesse meio tempo).
    /// </summary>
    private static void AnnounceMeshAsset(InspectorContext ctx, string assetPath)
    {
        var engine = ctx.Engine;
        var registry = ctx.Registry;
        var target = ctx.Entity;

        ctx.AnnounceAsset(assetPath, hash =>
        {
            if (!registry.IsAlive(target)) return;
            if (!registry.HasComponentByType(target, typeof(MeshComponent))) return;

            ref var updatedMesh = ref registry.GetComponent<MeshComponent>(target);
            updatedMesh.AssetHash = hash;

            if (!registry.HasComponentByType(target, typeof(NetworkIdentityComponent))) return;
            int netId = registry.GetComponent<NetworkIdentityComponent>(target).NetworkId;
            engine.GetModule<NetworkModule>()?.Replication?.SendUpdateMesh(netId, (int)updatedMesh.Type, hash);
        });
    }
}
