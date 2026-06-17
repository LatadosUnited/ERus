using System;
using System.Numerics;
using ImGuiNET;
using ERus.Engine.Core;
using ERus.Engine.Modules;
using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Panels;

public class GameViewWindow : EditorWindow
{
    private readonly ERus.Engine.Core.Engine _engine;

    public GameViewWindow(ERus.Engine.Core.Engine engine) : base("Game")
    {
        _engine = engine;
    }

    protected override void DrawContent()
    {
        var size = ImGui.GetContentRegionAvail();
        var graphics = _engine?.GetModule<GraphicsModule>();

        if (graphics == null)
        {
            ImGui.Text("Módulo Gráfico offline.");
            return;
        }

        graphics.GameViewSize = new Vector2(size.X, size.Y);

        var textureId = graphics.GameTextureId;
        if (textureId == 0)
        {
            ImGui.Text("Nenhuma câmera renderizando ou inicializando OpenGL...");
            return;
        }

        var cursorPos = ImGui.GetCursorScreenPos();
        ImGui.Image((IntPtr)textureId, size, new Vector2(0, 1), new Vector2(1, 0));
    }
}


