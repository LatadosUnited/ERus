using System;
using ImGuiNET;
using System.Numerics;

namespace ERus.Editor.EditorUI.Inspector;

public enum TextureSlotResult
{
    Unchanged,
    Assigned,
    Cleared
}

/// <summary>
/// Slot de textura compartilhado por Material e Sprite Renderer: preview clicavel
/// com alvo de drag-and-drop, nome curto do arquivo e botao de limpar.
/// </summary>
public static class TextureSlot
{
    private const float PreviewSize = 48f;
    private const int MaxNameLength = 20;

    /// <summary>
    /// Desenha o slot. O chamador deve ter aberto a linha com
    /// <see cref="InspectorContext.PropertyLabel"/> e deve fazer PopItemWidth depois.
    /// <paramref name="newGuid"/> so e relevante quando o retorno nao e Unchanged;
    /// <paramref name="droppedPath"/> so quando o retorno e Assigned.
    /// </summary>
    public static TextureSlotResult Draw(InspectorContext ctx, string id, Guid textureGuid,
        out Guid newGuid, out string droppedPath)
    {
        newGuid = textureGuid;
        droppedPath = string.Empty;
        var result = TextureSlotResult.Unchanged;

        string texPath = ctx.GetAssetPath(textureGuid, "(Nenhuma)", "(Desconhecido)");

        var assetMgr = ERus.Engine.Assets.AssetManager.Get();
        var texObj = textureGuid != Guid.Empty ? assetMgr.LoadTextureByGuid(textureGuid) : null;
        IntPtr texPtr = (IntPtr)(texObj?.Id ?? assetMgr.WhiteTexture.Id);

        ImGui.BeginGroup();
        ImGui.Image(texPtr, new Vector2(PreviewSize, PreviewSize), new Vector2(0, 1), new Vector2(1, 0));

        if (InspectorContext.TryAcceptAssetDrop(AssetExtensions.Textures, out string dropped))
        {
            var guid = ctx.ResolveAssetGuid(dropped);
            if (guid.HasValue) newGuid = guid.Value;
            droppedPath = dropped;
            result = TextureSlotResult.Assigned;
        }

        ImGui.SameLine();
        ImGui.BeginGroup();
        string shortName = System.IO.Path.GetFileName(texPath);
        ImGui.TextUnformatted(shortName.Length > MaxNameLength ? shortName.Substring(0, MaxNameLength - 3) + "..." : shortName);

        if (textureGuid != Guid.Empty && ImGui.Button($"Remover##{id}"))
        {
            newGuid = Guid.Empty;
            droppedPath = string.Empty;
            result = TextureSlotResult.Cleared;
        }
        ImGui.EndGroup();
        ImGui.EndGroup();

        return result;
    }
}
