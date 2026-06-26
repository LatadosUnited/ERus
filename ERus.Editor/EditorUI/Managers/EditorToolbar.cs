using ImGuiNET;
using System;
using System.Numerics;

namespace ERus.Editor.EditorUI.Managers;

public class EditorToolbar
{
    private ERus.Engine.Core.Engine _engine;
    private EditorUIController _controller;
    private EditorNetworkMenu _networkMenu;

    private string _tempScenePath = "Assets/_temp_play.scene";

    public EditorToolbar(EditorUIController controller, ERus.Engine.Core.Engine engine)
    {
        _controller = controller;
        _engine = engine;
        _networkMenu = new EditorNetworkMenu(engine);
    }

    public void Draw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 12));

        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("New Scene"))
                {
                    _engine.GetModule<ERus.Engine.Modules.ECSModule>().ActiveScene.Registry.Clear();
                }
                if (ImGui.MenuItem("Save Scene (Scene1.scene)"))
                {
                    var scene = _engine.GetModule<ERus.Engine.Modules.ECSModule>().ActiveScene;
                    string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.Environment.CurrentDirectory, "Assets", "Scene1.scene"));
                    ERus.Engine.ECS.SceneSerializer.SaveScene(path, scene);
                    _ = _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.NetworkManager?.AssetSync?.AnnounceAssetAsync(path);
                }
                if (ImGui.MenuItem("Load Scene (Scene1.scene)"))
                {
                    var scene = _engine.GetModule<ERus.Engine.Modules.ECSModule>().ActiveScene;
                    string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.Environment.CurrentDirectory, "Assets", "Scene1.scene"));
                    ERus.Engine.ECS.SceneSerializer.LoadScene(path, scene);
                }
                ImGui.Separator();
                if (ImGui.MenuItem("Exit"))
                {
                    Environment.Exit(0);
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Window"))
            {
                if (ImGui.MenuItem("Input Mapping"))
                {
                    _controller.WindowManager.InputMap.IsOpen = true;
                }

                if (ImGui.BeginMenu("Layouts"))
                {
                    if (ImGui.MenuItem("Unity Style"))
                    {
                        _controller.RequestLoadUnityLayout();
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Edit"))
            {
                if (ImGui.MenuItem("Undo", "Ctrl+Z", false, _controller.UndoSystem.CanUndo))
                    _controller.UndoSystem.Undo();
                if (ImGui.MenuItem("Redo", "Ctrl+Shift+Z", false, _controller.UndoSystem.CanRedo))
                    _controller.UndoSystem.Redo();
                ImGui.EndMenu();
            }

            _networkMenu.Draw();

            DrawPlayControls();

            ImGui.EndMainMenuBar();
        }

        ImGui.PopStyleVar();
    }

    private void DrawPlayControls()
    {
        float menuBarWidth = ImGui.GetWindowWidth();
        float buttonWidth = 60.0f;
        float totalButtonsWidth = (buttonWidth * 3) + (ImGui.GetStyle().ItemSpacing.X * 2);
        
        ImGui.SetCursorPosX((menuBarWidth * 0.5f) - (totalButtonsWidth * 0.5f));

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
        var colors = ImGui.GetStyle().Colors;
        var buttonHovered = colors[(int)ImGuiCol.ButtonHovered];
        var buttonActive = colors[(int)ImGuiCol.ButtonActive];
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(buttonHovered.X, buttonHovered.Y, buttonHovered.Z, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(buttonActive.X, buttonActive.Y, buttonActive.Z, 0.5f));

        bool isPlay = _engine.State == ERus.Engine.Core.EngineState.Play;
        bool isPause = _engine.State == ERus.Engine.Core.EngineState.Pause;

        if (isPlay || isPause) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.2f, 0.8f, 0.2f, 1.0f));
        if (ImGui.Button($"Play", new Vector2(buttonWidth, 0)))
        {
            if (_engine.State == ERus.Engine.Core.EngineState.Edit)
            {
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.Environment.CurrentDirectory, _tempScenePath));
                var scene = _engine.GetModule<ERus.Engine.Modules.ECSModule>().ActiveScene;
                ERus.Engine.ECS.SceneSerializer.SaveScene(path, scene);
                _engine.State = ERus.Engine.Core.EngineState.Play;
                _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.Replication?.SendEngineState((byte)ERus.Engine.Core.EngineState.Play);
            }
            else if (_engine.State == ERus.Engine.Core.EngineState.Pause)
            {
                _engine.State = ERus.Engine.Core.EngineState.Play;
                _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.Replication?.SendEngineState((byte)ERus.Engine.Core.EngineState.Play);
            }
        }
        if (isPlay || isPause) ImGui.PopStyleColor();

        ImGui.SameLine();

        if (isPause) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.6f, 0.2f, 1.0f));
        if (ImGui.Button($"Pause", new Vector2(buttonWidth, 0)))
        {
            if (_engine.State == ERus.Engine.Core.EngineState.Play)
            {
                _engine.State = ERus.Engine.Core.EngineState.Pause;
                _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.Replication?.SendEngineState((byte)ERus.Engine.Core.EngineState.Pause);
            }
            else if (_engine.State == ERus.Engine.Core.EngineState.Pause)
            {
                _engine.State = ERus.Engine.Core.EngineState.Play;
                _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.Replication?.SendEngineState((byte)ERus.Engine.Core.EngineState.Play);
            }
        }
        if (isPause) ImGui.PopStyleColor();

        ImGui.SameLine();

        if (ImGui.Button($"Stop", new Vector2(buttonWidth, 0)))
        {
            if (_engine.State != ERus.Engine.Core.EngineState.Edit)
            {
                _engine.State = ERus.Engine.Core.EngineState.Edit;
                _engine.GetModule<ERus.Engine.Modules.NetworkModule>()?.Replication?.SendEngineState((byte)ERus.Engine.Core.EngineState.Edit);
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.Environment.CurrentDirectory, _tempScenePath));
                var scene = _engine.GetModule<ERus.Engine.Modules.ECSModule>().ActiveScene;
                ERus.Engine.ECS.SceneSerializer.LoadScene(path, scene);
                EditorServices.Selection.ClearSelection();
            }
        }

        ImGui.PopStyleColor(3);
    }
}


