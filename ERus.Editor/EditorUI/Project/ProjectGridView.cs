using ImGuiNET;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using ERus.Engine.Assets;
using ERus.Engine.Core;
using ERus.Engine.ECS;
using ERus.Engine.Modules;

namespace ERus.Editor.EditorUI.Project;

public static class ProjectGridView
{
    public static void Draw(
        ref string currentPath,
        ref string? selectedFile,
        float thumbnailSize,
        string searchQuery,
        Engine.Core.Engine engine,
        ref bool openRenamePopup,
        Action<string, string> onOpenFileOrScene)
    {
        var dirs = Directory.GetDirectories(currentPath)
            .Where(d => string.IsNullOrEmpty(searchQuery) || Path.GetFileName(d).Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var files = Directory.GetFiles(currentPath)
            .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .Where(f => string.IsNullOrEmpty(searchQuery) || Path.GetFileName(f).Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        float padding = 8.0f;
        float cardWidth = thumbnailSize + 16.0f;
        float cardHeight = thumbnailSize + 32.0f;
        float availWidth = ImGui.GetContentRegionAvail().X;
        int columns = Math.Max(1, (int)(availWidth / (cardWidth + padding)));

        int itemIndex = 0;

        // Renderizar Pastas em Grid
        foreach (var dir in dirs)
        {
            var dirName = Path.GetFileName(dir);
            bool isSelected = selectedFile == dir;

            ImGui.PushID($"Dir_{dirName}_{itemIndex}");
            DrawFolderCard(dir, dirName, cardWidth, cardHeight, isSelected, thumbnailSize, ref currentPath, ref selectedFile, engine, ref openRenamePopup);
            ImGui.PopID();

            itemIndex++;
            if (itemIndex % columns != 0)
            {
                ImGui.SameLine(0, padding);
            }
        }

        // Renderizar Arquivos em Grid
        var assetManager = AssetManager.Get();
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            bool isSelected = selectedFile == file;

            ImGui.PushID($"File_{fileName}_{itemIndex}");
            DrawFileCard(file, fileName, cardWidth, cardHeight, isSelected, thumbnailSize, ref selectedFile, assetManager, engine, ref openRenamePopup, onOpenFileOrScene);
            ImGui.PopID();

            itemIndex++;
            if (itemIndex % columns != 0)
            {
                ImGui.SameLine(0, padding);
            }
        }

        if (itemIndex % columns != 0)
        {
            ImGui.NewLine();
        }
    }

    private static void DrawFolderCard(
        string dir,
        string dirName,
        float cardWidth,
        float cardHeight,
        bool isSelected,
        float thumbnailSize,
        ref string currentPath,
        ref string? selectedFile,
        Engine.Core.Engine engine,
        ref bool openRenamePopup)
    {
        Vector2 cardSize = new Vector2(cardWidth, cardHeight);
        var drawList = ImGui.GetWindowDrawList();
        Vector2 pMin = ImGui.GetCursorScreenPos();
        Vector2 pMax = pMin + cardSize;

        if (isSelected)
        {
            drawList.AddRectFilled(pMin, pMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.4f, 0.8f, 0.35f)), 4.0f);
            drawList.AddRect(pMin, pMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.6f, 1.0f, 0.8f)), 4.0f);
        }

        if (ImGui.InvisibleButton($"##FolderBtn_{dirName}", cardSize))
        {
            selectedFile = dir;
        }

        if (ImGui.IsItemHovered())
        {
            drawList.AddRect(pMin, pMax, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.3f)), 4.0f);
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                currentPath = dir;
            }
        }

        // Drag Drop Target para mover arquivos para a pasta ou salvar prefab
        if (ImGui.BeginDragDropTarget())
        {
            HandleFolderDropTarget(dir, engine);
            ImGui.EndDragDropTarget();
        }

        // Ícone da Pasta
        Vector2 iconCenter = new Vector2(pMin.X + cardWidth * 0.5f, pMin.Y + thumbnailSize * 0.45f);
        string folderIcon = FontAwesome.Folder;
        Vector2 textSize = ImGui.CalcTextSize(folderIcon);
        drawList.AddText(new Vector2(iconCenter.X - textSize.X * 0.5f, iconCenter.Y - textSize.Y * 0.5f), ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.85f, 0.3f, 1.0f)), folderIcon);

        // Texto do nome
        string displayName = ProjectAssetUtils.TruncateString(dirName, 14);
        Vector2 nameSize = ImGui.CalcTextSize(displayName);
        drawList.AddText(new Vector2(pMin.X + (cardWidth - nameSize.X) * 0.5f, pMin.Y + thumbnailSize + 4.0f), ImGui.ColorConvertFloat4ToU32(Vector4.One), displayName);

        // Context Menu
        DrawItemContextMenu(dir, dirName, isDirectory: true, ref selectedFile, engine, ref openRenamePopup);
    }

    private static void DrawFileCard(
        string file,
        string fileName,
        float cardWidth,
        float cardHeight,
        bool isSelected,
        float thumbnailSize,
        ref string? selectedFile,
        AssetManager assetManager,
        Engine.Core.Engine engine,
        ref bool openRenamePopup,
        Action<string, string> onOpenFileOrScene)
    {
        Vector2 cardSize = new Vector2(cardWidth, cardHeight);
        var drawList = ImGui.GetWindowDrawList();
        Vector2 pMin = ImGui.GetCursorScreenPos();
        Vector2 pMax = pMin + cardSize;

        bool isImage = ProjectAssetUtils.IsImageFile(file);

        if (isSelected)
        {
            drawList.AddRectFilled(pMin, pMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.4f, 0.8f, 0.35f)), 4.0f);
            drawList.AddRect(pMin, pMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.6f, 1.0f, 0.8f)), 4.0f);
        }

        if (ImGui.InvisibleButton($"##FileBtn_{fileName}", cardSize))
        {
            selectedFile = file;
        }

        bool isHovered = ImGui.IsItemHovered();
        if (isHovered)
        {
            drawList.AddRect(pMin, pMax, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.3f)), 4.0f);
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                onOpenFileOrScene(file, fileName);
            }

            DrawFileTooltip(file, fileName, isImage, assetManager);
        }

        // Drag Drop Source
        if (ImGui.BeginDragDropSource())
        {
            ERus.Editor.EditorUI.Managers.DragDropState.DraggedPayload = file;
            ImGui.SetDragDropPayload("ASSET_PATH", IntPtr.Zero, 0);

            if (isImage)
            {
                var tex = assetManager.LoadTexture(file);
                if (tex != null)
                {
                    ImGui.Image((IntPtr)tex.Id, new Vector2(36, 36), new Vector2(0, 1), new Vector2(1, 0));
                    ImGui.SameLine();
                }
            }
            ImGui.Text(fileName);
            ImGui.EndDragDropSource();
        }

        // Miniatura ou Ícone
        Vector2 thumbAreaMin = new Vector2(pMin.X + 8.0f, pMin.Y + 6.0f);
        Vector2 thumbAreaMax = new Vector2(pMin.X + cardWidth - 8.0f, pMin.Y + thumbnailSize + 2.0f);

        if (isImage)
        {
            var tex = assetManager.LoadTexture(file);
            if (tex != null)
            {
                drawList.AddImage((IntPtr)tex.Id, thumbAreaMin, thumbAreaMax, new Vector2(0, 1), new Vector2(1, 0));
                drawList.AddRect(thumbAreaMin, thumbAreaMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.2f, 0.2f, 0.5f)));
            }
            else
            {
                drawList.AddRectFilled(thumbAreaMin, thumbAreaMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.18f, 1.0f)), 2.0f);
                drawList.AddText(new Vector2(thumbAreaMin.X + 6, thumbAreaMin.Y + 6), ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.7f, 0.7f, 1.0f)), "[IMG]");
            }
        }
        else
        {
            drawList.AddRectFilled(thumbAreaMin, thumbAreaMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.13f, 0.13f, 0.16f, 1.0f)), 4.0f);
            var (icon, iconColor) = ProjectAssetUtils.GetFileIconAndColor(file);
            Vector2 iconSize = ImGui.CalcTextSize(icon);
            Vector2 center = (thumbAreaMin + thumbAreaMax) * 0.5f;
            drawList.AddText(new Vector2(center.X - iconSize.X * 0.5f, center.Y - iconSize.Y * 0.5f), ImGui.ColorConvertFloat4ToU32(iconColor), icon);
        }

        // Nome do arquivo
        string displayName = ProjectAssetUtils.TruncateString(fileName, 14);
        Vector2 nameSize = ImGui.CalcTextSize(displayName);
        drawList.AddText(new Vector2(pMin.X + (cardWidth - nameSize.X) * 0.5f, pMin.Y + thumbnailSize + 6.0f), ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.9f, 0.9f, 1.0f)), displayName);

        // Context Menu
        DrawItemContextMenu(file, fileName, isDirectory: false, ref selectedFile, engine, ref openRenamePopup);
    }

    public static void DrawFileTooltip(string file, string fileName, bool isImage, AssetManager assetManager)
    {
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(fileName);
        if (isImage)
        {
            var tex = assetManager.LoadTexture(file);
            if (tex != null)
            {
                ImGui.Image((IntPtr)tex.Id, new Vector2(128, 128), new Vector2(0, 1), new Vector2(1, 0));
            }
        }

        try
        {
            var fi = new FileInfo(file);
            float sizeKb = fi.Length / 1024.0f;
            ImGui.TextDisabled($"Tamanho: {sizeKb:F1} KB");
        }
        catch { }

        ImGui.EndTooltip();
    }

    public static void DrawItemContextMenu(
        string path,
        string itemName,
        bool isDirectory,
        ref string? selectedFile,
        Engine.Core.Engine engine,
        ref bool openRenamePopup)
    {
        if (ImGui.BeginPopupContextItem($"ContextMenu_{itemName}"))
        {
            selectedFile = path;

            if (ImGui.MenuItem("Renomear"))
            {
                openRenamePopup = true;
            }

            if (ImGui.MenuItem("Mostrar no Explorer"))
            {
                ProjectAssetUtils.ShowInExplorer(path);
            }

            ImGui.Separator();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
            if (ImGui.MenuItem("Deletar"))
            {
                ProjectFileOperations.DeleteItem(path, engine);
                selectedFile = null;
            }
            ImGui.PopStyleColor();
            ImGui.EndPopup();
        }
    }

    public static void HandleFolderDropTarget(string targetDir, Engine.Core.Engine engine)
    {
        var payload = ImGui.AcceptDragDropPayload("ASSET_PATH");
        unsafe
        {
            if (payload.NativePtr != null)
            {
                string sourceFile = ERus.Editor.EditorUI.Managers.DragDropState.DraggedPayload;
                ProjectFileOperations.MoveItem(sourceFile, targetDir, engine);
            }

            var entityPayload = ImGui.AcceptDragDropPayload("ENTITY");
            if (entityPayload.NativePtr != null)
            {
                int id = *(int*)entityPayload.Data;
                ProjectFileOperations.SaveEntityAsPrefab(targetDir, id, engine);
            }
        }
    }
}
