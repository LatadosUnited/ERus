using System;
using System.IO;
using System.Numerics;
using ImGuiNET;
using ERus.Editor.EditorUI.Project;
using ERus.Engine.Core;
using ERus.Engine.ECS;
using ERus.Engine.Modules;
using ERus.Engine.Scripting;

namespace ERus.Editor.EditorUI.Panels;

public enum ProjectViewMode
{
    Grid,
    List
}

public class ProjectWindow : EditorWindow
{
    private readonly ERus.Engine.Core.Engine _engine;
    private string _basePath;
    private string _currentPath;
    private string? _selectedFile;

    private AssetCreationType _createMode = AssetCreationType.Folder;
    private string _newItemName = "";
    private string _renameItemName = "";

    private ProjectViewMode _viewMode = ProjectViewMode.Grid;
    private float _thumbnailSize = 72.0f;
    private string _searchQuery = "";

    public ProjectWindow(ERus.Engine.Core.Engine engine) : base("Project")
    {
        _engine = engine;
        _basePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Assets"));
        if (!Directory.Exists(_basePath))
            Directory.CreateDirectory(_basePath);

        _currentPath = _basePath;
    }

    protected override void DrawContent()
    {
        bool openRenamePopup = false;

        DrawToolbar();

        if (!Directory.Exists(_currentPath)) return;

        ImGui.BeginChild("ProjectFilesScrollArea", new Vector2(0, 0), ImGuiChildFlags.None);

        if (_viewMode == ProjectViewMode.Grid)
        {
            ProjectGridView.Draw(ref _currentPath, ref _selectedFile, _thumbnailSize, _searchQuery, _engine, ref openRenamePopup, OpenFileOrScene);
        }
        else
        {
            ProjectListView.Draw(ref _currentPath, ref _selectedFile, _searchQuery, _engine, ref openRenamePopup, OpenFileOrScene);
        }

        // Atalhos de teclado (F2, Delete)
        HandleKeyboardShortcuts(ref openRenamePopup);

        // Área vazia para drop de Prefab e Menu de Contexto de Criação
        DrawEmptySpaceDropAndContextMenu();

        ImGui.EndChild();

        // Modais de Criação e Renomeação
        DrawModals(ref openRenamePopup);
    }

    private void DrawToolbar()
    {
        bool canGoBack = _currentPath != _basePath;
        if (!canGoBack) ImGui.BeginDisabled();
        if (ImGui.Button($"{FontAwesome.FolderOpen} <- Back"))
        {
            _currentPath = Directory.GetParent(_currentPath)?.FullName ?? _basePath;
            if (!_currentPath.StartsWith(_basePath)) _currentPath = _basePath;
        }
        if (!canGoBack) ImGui.EndDisabled();

        if (canGoBack && ImGui.BeginDragDropTarget())
        {
            var parentDirInfo = Directory.GetParent(_currentPath);
            if (parentDirInfo != null && parentDirInfo.FullName.StartsWith(_basePath))
            {
                var payload = ImGui.AcceptDragDropPayload("ASSET_PATH");
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        string sourceFile = ERus.Editor.EditorUI.Managers.DragDropState.DraggedPayload;
                        ProjectFileOperations.MoveItem(sourceFile, parentDirInfo.FullName, _engine);
                    }
                }
            }
            ImGui.EndDragDropTarget();
        }

        ImGui.SameLine();

        // Toggle Grid / List
        if (ImGui.Button(_viewMode == ProjectViewMode.Grid ? $"{FontAwesome.Cube} Grid" : $"{FontAwesome.File} List"))
        {
            _viewMode = _viewMode == ProjectViewMode.Grid ? ProjectViewMode.List : ProjectViewMode.Grid;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Alternar entre visualização em Grade e Lista");

        ImGui.SameLine();

        // Slider de Zoom no Grid
        if (_viewMode == ProjectViewMode.Grid)
        {
            ImGui.SetNextItemWidth(100);
            ImGui.SliderFloat("##ThumbSize", ref _thumbnailSize, 48.0f, 128.0f, "%.0f px");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Ajustar tamanho das miniaturas");
            ImGui.SameLine();
        }

        // Filtro de Busca
        ImGui.SetNextItemWidth(140);
        ImGui.InputTextWithHint("##ProjectSearch", "Filtrar...", ref _searchQuery, 128);
        if (!string.IsNullOrEmpty(_searchQuery))
        {
            ImGui.SameLine();
            if (ImGui.Button("X")) _searchQuery = "";
        }

        ImGui.SameLine();
        if (ImGui.Button($"{FontAwesome.Save} Scan"))
        {
            _engine.AssetDatabase.Scan();
            ConsoleLog.Log("[Project] AssetDatabase re-escaneado com sucesso.");
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Re-escanear arquivos e atualizar metadados (.meta)");

        // Caminho
        string displayPath = _currentPath.Replace(_basePath, "Assets").Replace('\\', '/');
        if (string.IsNullOrEmpty(displayPath)) displayPath = "Assets";
        ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), $" {displayPath}");
        ImGui.Separator();
    }

    private void HandleKeyboardShortcuts(ref bool openRenamePopup)
    {
        if (!string.IsNullOrEmpty(_selectedFile) && ImGui.IsKeyPressed(ImGuiKey.F2) && !ImGui.GetIO().WantTextInput)
        {
            openRenamePopup = true;
        }

        if (!string.IsNullOrEmpty(_selectedFile) && ImGui.IsKeyPressed(ImGuiKey.Delete) && !ImGui.GetIO().WantTextInput)
        {
            ProjectFileOperations.DeleteItem(_selectedFile, _engine);
            _selectedFile = null;
        }
    }

    private void DrawEmptySpaceDropAndContextMenu()
    {
        float remainingH = ImGui.GetContentRegionAvail().Y;
        if (remainingH > 20.0f)
        {
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, remainingH));
            if (ImGui.BeginDragDropTarget())
            {
                var entityPayload = ImGui.AcceptDragDropPayload("ENTITY");
                unsafe
                {
                    if (entityPayload.NativePtr != null)
                    {
                        int id = *(int*)entityPayload.Data;
                        ProjectFileOperations.SaveEntityAsPrefab(_currentPath, id, _engine);
                    }
                }
                ImGui.EndDragDropTarget();
            }
        }

        bool openCreatePopup = false;
        if (ImGui.BeginPopupContextWindow("ProjectWindowContextMenu", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
        {
            if (ImGui.MenuItem($"{FontAwesome.Folder} Create Folder")) { _createMode = AssetCreationType.Folder; openCreatePopup = true; }
            if (ImGui.MenuItem($"{FontAwesome.Code} Create Script (.cs)")) { _createMode = AssetCreationType.Script; openCreatePopup = true; }
            if (ImGui.MenuItem($"{FontAwesome.Camera} Create Scene (.scene)")) { _createMode = AssetCreationType.Scene; openCreatePopup = true; }
            if (ImGui.MenuItem($"{FontAwesome.File} Create Input Map (.json)")) { _createMode = AssetCreationType.InputProfile; openCreatePopup = true; }
            ImGui.EndPopup();
        }

        if (openCreatePopup)
        {
            _newItemName = "";
            ImGui.OpenPopup("CreateNewItemPopup");
        }
    }

    private void DrawModals(ref bool openRenamePopup)
    {
        // Modal de Criação
        if (ImGui.BeginPopupModal("CreateNewItemPopup", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text($"Novo(a) {_createMode.ToString()}:");
            ImGui.InputText("##NewItemName", ref _newItemName, 256);

            if (ImGui.Button("Criar", new Vector2(120, 0)) || ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                if (!string.IsNullOrWhiteSpace(_newItemName))
                {
                    ProjectFileOperations.CreateAsset(_createMode, _currentPath, _newItemName, _engine);
                }
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancelar", new Vector2(120, 0)) || ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (openRenamePopup && !string.IsNullOrEmpty(_selectedFile))
        {
            _renameItemName = Path.GetFileName(_selectedFile);
            ImGui.OpenPopup("RenameItemPopup");
        }

        // Modal de Renomeação
        if (ImGui.BeginPopupModal("RenameItemPopup", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Renomear:");
            ImGui.InputText("##RenameItemName", ref _renameItemName, 256);

            if (ImGui.Button("Renomear", new Vector2(120, 0)) || ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                if (!string.IsNullOrWhiteSpace(_renameItemName) && !string.IsNullOrEmpty(_selectedFile))
                {
                    if (ProjectFileOperations.RenameItem(_selectedFile, _renameItemName, _engine, out string newPath))
                    {
                        _selectedFile = newPath;
                    }
                }
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancelar", new Vector2(120, 0)) || ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void OpenFileOrScene(string file, string fileName)
    {
        if (fileName.EndsWith(".scene", StringComparison.OrdinalIgnoreCase))
        {
            var scene = _engine.GetModule<ECSModule>()?.ActiveScene;
            if (scene != null)
            {
                SceneSerializer.LoadScene(file, scene);
                ConsoleLog.Log($"[Project] Carregando cena: {fileName}");
            }
        }
        else
        {
            ProjectAssetUtils.OpenFileExternally(file);
        }
    }

    public void ImportFiles(string[] files)
    {
        ProjectFileOperations.ImportFiles(files, _currentPath, _engine);
    }
}
