using ImGuiNET;
using System.IO;
using System;

namespace ERus.Editor.EditorUI.Panels;

public class ProjectWindow : EditorWindow
{
    private string _basePath;
    private string _currentPath;
    private string? _selectedFile;
    private readonly ERus.Engine.Core.Engine _engine;
    
    private enum CreateMode { None, Folder, Script, Scene, InputProfile }
    private CreateMode _createMode = CreateMode.None;
    private string _newItemName = "";
    private string _renameItemName = "";

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

        if (_currentPath != _basePath)
        {
            if (ImGui.Button("<- Back"))
            {
                _currentPath = Directory.GetParent(_currentPath)?.FullName ?? _basePath;
                if (!_currentPath.StartsWith(_basePath)) _currentPath = _basePath;
            }
            ImGui.Separator();
        }

        string displayPath = _currentPath.Replace(_basePath, "Assets").Replace('\\', '/');
        ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.8f, 0.2f, 1.0f), displayPath);
        ImGui.Separator();

        if (!Directory.Exists(_currentPath)) return;

        var dirs = Directory.GetDirectories(_currentPath);
        foreach (var dir in dirs)
        {
            var dirName = Path.GetFileName(dir);
            if (ImGui.Selectable($"[DIR] {dirName}", false, ImGuiSelectableFlags.AllowDoubleClick))
            {
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    _currentPath = dir;
                }
            }
            
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("ASSET_PATH");
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        string sourceFile = ERus.Editor.EditorUI.Managers.DragDropState.DraggedPayload;
                        if (File.Exists(sourceFile))
                        {
                            string fileName = Path.GetFileName(sourceFile);
                            string destFile = Path.Combine(dir, fileName);
                            if (sourceFile != destFile)
                            {
                                File.Move(sourceFile, destFile);
                                ERus.Engine.Scripting.ConsoleLog.Log($"[Project] Arquivo movido: {fileName} -> {dirName}");
                                
                                string sourceMeta = sourceFile + ".meta";
                                if (File.Exists(sourceMeta))
                                {
                                    File.Move(sourceMeta, destFile + ".meta");
                                }
                                
                                _engine.AssetDatabase.Scan();
                            }
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }
        }

        var files = Directory.GetFiles(_currentPath);
        foreach (var file in files)
        {
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            
            var fileName = Path.GetFileName(file);
            bool isSelected = _selectedFile == file;
            if (ImGui.Selectable($"      {fileName}", isSelected, ImGuiSelectableFlags.AllowDoubleClick))
            {
                _selectedFile = file;
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    if (fileName.EndsWith(".scene"))
                    {
                        var scene = _engine.GetModule<ERus.Engine.Modules.ECSModule>().ActiveScene;
                        ERus.Engine.ECS.SceneSerializer.LoadScene(file, scene);
                        ERus.Engine.Scripting.ConsoleLog.Log($"[Project] Carregando cena: {fileName}");
                    }
                }
            }
            
            if (ImGui.BeginDragDropSource())
            {
                ERus.Editor.EditorUI.Managers.DragDropState.DraggedPayload = file; // Usar caminho absoluto para mover
                ImGui.SetDragDropPayload("ASSET_PATH", IntPtr.Zero, 0);
                ImGui.Text(fileName);
                ImGui.EndDragDropSource();
            }
            
            if (ImGui.BeginPopupContextItem($"ContextMenu_{fileName}"))
            {
                _selectedFile = file;

                if (ImGui.MenuItem("Rename File"))
                {
                    openRenamePopup = true;
                }

                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                if (ImGui.MenuItem("Delete File"))
                {
                    File.Delete(file);
                    string metaFile = file + ".meta";
                    if (File.Exists(metaFile)) File.Delete(metaFile);
                    
                    _engine.AssetDatabase.Scan();
                    ERus.Engine.Scripting.ConsoleLog.Log($"[Project] Arquivo deletado: {file}");
                }
                ImGui.PopStyleColor();
                ImGui.EndPopup();
            }
        }

        // Atalho Teclado: Renomear Arquivo
        if (!string.IsNullOrEmpty(_selectedFile) && ImGui.IsKeyPressed(ImGuiKey.F2) && !ImGui.GetIO().WantTextInput)
        {
            openRenamePopup = true;
        }

        // Atalho Teclado: Deletar Arquivo
        if (!string.IsNullOrEmpty(_selectedFile) && ImGui.IsKeyPressed(ImGuiKey.Delete) && !ImGui.GetIO().WantTextInput)
        {
            if (File.Exists(_selectedFile))
            {
                File.Delete(_selectedFile);
                string metaFile = _selectedFile + ".meta";
                if (File.Exists(metaFile)) File.Delete(metaFile);
                
                _engine.AssetDatabase.Scan();
                ERus.Engine.Scripting.ConsoleLog.Log($"[Project] Arquivo deletado via teclado: {_selectedFile}");
                _selectedFile = null;
            }
        }
        
        // Menu de Contexto Global para Criação (clique direito no espaço vazio)
        bool openCreatePopup = false;
        if (ImGui.BeginPopupContextWindow("ProjectWindowContextMenu", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
        {
            if (ImGui.MenuItem("Create Folder")) { _createMode = CreateMode.Folder; openCreatePopup = true; }
            if (ImGui.MenuItem("Create Script (.cs)")) { _createMode = CreateMode.Script; openCreatePopup = true; }
            if (ImGui.MenuItem("Create Scene (.scene)")) { _createMode = CreateMode.Scene; openCreatePopup = true; }
            if (ImGui.MenuItem("Create Input Map (.json)")) { _createMode = CreateMode.InputProfile; openCreatePopup = true; }
            ImGui.EndPopup();
        }

        if (openCreatePopup)
        {
            _newItemName = "";
            ImGui.OpenPopup("CreateNewItemPopup");
        }

        // Modal de Criação
        if (ImGui.BeginPopupModal("CreateNewItemPopup", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text($"New {_createMode.ToString()}:");
            ImGui.InputText("##NewItemName", ref _newItemName, 256);

            if (ImGui.Button("Create", new System.Numerics.Vector2(120, 0)) || ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                if (!string.IsNullOrWhiteSpace(_newItemName))
                {
                    ExecuteCreation(_newItemName);
                }
                ImGui.CloseCurrentPopup();
                _createMode = CreateMode.None;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0)) || ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                ImGui.CloseCurrentPopup();
                _createMode = CreateMode.None;
            }
            ImGui.EndPopup();
        }

        if (openRenamePopup && !string.IsNullOrEmpty(_selectedFile))
        {
            _renameItemName = Path.GetFileName(_selectedFile);
            ImGui.OpenPopup("RenameItemPopup");
        }

        // Modal de Renomear
        if (ImGui.BeginPopupModal("RenameItemPopup", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Rename File:");
            ImGui.InputText("##RenameItemName", ref _renameItemName, 256);

            if (ImGui.Button("Rename", new System.Numerics.Vector2(120, 0)) || ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                if (!string.IsNullOrWhiteSpace(_renameItemName) && !string.IsNullOrEmpty(_selectedFile))
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(_selectedFile) ?? _currentPath;
                        string newPath = Path.Combine(dir, _renameItemName);
                        if (_selectedFile != newPath)
                        {
                            File.Move(_selectedFile, newPath);
                            string oldMeta = _selectedFile + ".meta";
                            if (File.Exists(oldMeta))
                            {
                                File.Move(oldMeta, newPath + ".meta");
                            }

                            _engine.AssetDatabase.Scan();
                            ERus.Engine.Scripting.ConsoleLog.Log($"[Project] Arquivo renomeado de {Path.GetFileName(_selectedFile)} para {_renameItemName}");
                            _selectedFile = newPath;
                            _ = _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.NetworkManager?.AssetSync?.AnnounceAssetAsync(newPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        ERus.Engine.Scripting.ConsoleLog.Error($"[Project] Erro ao renomear: {ex.Message}");
                    }
                }
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0)) || ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void ExecuteCreation(string enteredName)
    {
        try
        {
            string cleanName = enteredName.Trim();
            if (string.IsNullOrWhiteSpace(cleanName)) return;

            string safeName = string.Join("_", cleanName.Split(Path.GetInvalidFileNameChars()));
            string fullPath = Path.Combine(_currentPath, safeName);

            if (_createMode == CreateMode.Folder)
            {
                Directory.CreateDirectory(fullPath);
                ERus.Engine.Scripting.ConsoleLog.Log($"[Project] Pasta criada: {fullPath}");
            }
            else if (_createMode == CreateMode.Script)
            {
                if (!fullPath.EndsWith(".cs")) fullPath += ".cs";
                string className = Path.GetFileNameWithoutExtension(fullPath);
                string template = $@"using ERus.Engine.ECS;
using ERus.Engine.Core;
using ERus.Engine.Scripting;
using System;

public class {className} : EntityScript
{{
    public override void OnUpdate(float deltaTime)
    {{
        // Seu código aqui
    }}
}}";
                File.WriteAllText(fullPath, template);
                ERus.Engine.Scripting.ConsoleLog.Log($"[Project] Script criado: {fullPath}");
                _ = _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.NetworkManager?.AssetSync?.AnnounceAssetAsync(fullPath);
            }
            else if (_createMode == CreateMode.Scene)
            {
                if (!fullPath.EndsWith(".scene")) fullPath += ".scene";
                string template = "{ \"Entities\": [] }";
                File.WriteAllText(fullPath, template);
                ERus.Engine.Scripting.ConsoleLog.Log($"[Project] Cena criada: {fullPath}");
                _ = _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.NetworkManager?.AssetSync?.AnnounceAssetAsync(fullPath);
            }
            else if (_createMode == CreateMode.InputProfile)
            {
                if (!fullPath.EndsWith(".json")) fullPath += ".json";
                string template = @"{
  ""Maps"": [
    {
      ""Name"": ""Player"",
      ""IsActive"": true,
      ""Actions"": []
    }
  ]
}";
                File.WriteAllText(fullPath, template);
                ERus.Engine.Scripting.ConsoleLog.Log($"[Project] Input Map criado: {fullPath}");
                _ = _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.NetworkManager?.AssetSync?.AnnounceAssetAsync(fullPath);
            }
        }
        catch (Exception ex)
        {
            ERus.Engine.Scripting.ConsoleLog.Error($"[Project] Erro ao criar item: {ex.Message}");
        }
    }
}


