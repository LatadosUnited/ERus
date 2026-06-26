using System;
using ImGuiNET;
using ERus.Editor.EditorUI.Panels;

namespace ERus.Editor.EditorUI.Managers;

public class EditorWindowManager
{
    private ERus.Engine.Core.Engine _engine;
    private EditorUIController _controller;

    public HierarchyWindow Hierarchy { get; private set; }
    public InspectorWindow Inspector { get; private set; }
    public ProjectWindow Project { get; private set; }
    public ConsoleWindow Console { get; private set; }
    public SceneViewWindow SceneView { get; private set; }
    public GameViewWindow GameView { get; private set; }
    public InputMapWindow InputMap { get; private set; }

    public EditorWindowManager(EditorUIController controller, ERus.Engine.Core.Engine engine)
    {
        _controller = controller;
        _engine = engine;

        Hierarchy = new HierarchyWindow(_controller, _engine);
        Inspector = new InspectorWindow(_controller, _engine);
        Project = new ProjectWindow(_engine);
        Console = new ConsoleWindow();
        SceneView = new SceneViewWindow(_controller, _engine);
        GameView = new GameViewWindow(_engine);
        InputMap = new InputMapWindow();
    }

    public void DrawWindows()
    {
        ImGui.Begin("Hierarchy");
        Hierarchy.DrawRawContent();
        ImGui.End();

        ImGui.Begin("Inspector");
        Inspector.DrawRawContent();
        ImGui.End();

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2(0, 0));
        ImGui.Begin("Scene");
        SceneView.DrawRawContent();
        ImGui.End();
        
        ImGui.Begin("Game");
        GameView.DrawRawContent();
        ImGui.End();
        ImGui.PopStyleVar();

        ImGui.Begin("Project");
        Project.DrawRawContent();
        ImGui.End();

        ImGui.Begin("Console");
        Console.DrawRawContent();
        ImGui.End();

        if (InputMap.IsOpen)
        {
            InputMap.DrawWindow();
        }
    }
}


