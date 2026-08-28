using ImGuiNET;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using ERus.Engine.Assets;
using ERus.Engine.Core;

namespace ERus.Editor.EditorUI.Project;

public static class ProjectListView
{
    public static void Draw(
        ref string currentPath,
        ref string? selectedFile,
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

        var assetManager = AssetManager.Get();

        // Pastas em Lista
        foreach (var dir in dirs)
        {
            var dirName = Path.GetFileName(dir);
            bool isSelected = selectedFile == dir;

            if (ImGui.Selectable($"{FontAwesome.Folder}  {dirName}", isSelected, ImGuiSelectableFlags.AllowDoubleClick))
            {
                selectedFile = dir;
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    currentPath = dir;
                }
            }

            if (ImGui.BeginDragDropTarget())
            {
                ProjectGridView.HandleFolderDropTarget(dir, engine);
                ImGui.EndDragDropTarget();
            }

            ProjectGridView.DrawItemContextMenu(dir, dirName, isDirectory: true, ref selectedFile, engine, ref openRenamePopup);
        }

        // Arquivos em Lista
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            bool isSelected = selectedFile == file;
            bool isImage = ProjectAssetUtils.IsImageFile(file);
            var (icon, _) = ProjectAssetUtils.GetFileIconAndColor(file);

            if (ImGui.Selectable($"{icon}  {fileName}", isSelected, ImGuiSelectableFlags.AllowDoubleClick))
            {
                selectedFile = file;
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    onOpenFileOrScene(file, fileName);
                }
            }

            if (ImGui.IsItemHovered())
            {
                ProjectGridView.DrawFileTooltip(file, fileName, isImage, assetManager);
            }

            if (ImGui.BeginDragDropSource())
            {
                ERus.Editor.EditorUI.Managers.DragDropState.DraggedPayload = file;
                ImGui.SetDragDropPayload("ASSET_PATH", IntPtr.Zero, 0);

                if (isImage)
                {
                    var tex = assetManager.LoadTexture(file);
                    if (tex != null)
                    {
                        ImGui.Image((IntPtr)tex.Id, new Vector2(32, 32), new Vector2(0, 1), new Vector2(1, 0));
                        ImGui.SameLine();
                    }
                }
                ImGui.Text(fileName);
                ImGui.EndDragDropSource();
            }

            ProjectGridView.DrawItemContextMenu(file, fileName, isDirectory: false, ref selectedFile, engine, ref openRenamePopup);
        }
    }
}
