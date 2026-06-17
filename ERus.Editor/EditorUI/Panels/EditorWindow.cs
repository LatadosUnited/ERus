using ImGuiNET;
using System.Numerics;

namespace ERus.Editor.EditorUI.Panels;

public abstract class EditorWindow
{
    public string Title { get; protected set; }

    protected EditorWindow(string title)
    {
        Title = title;
    }

    public void DrawRawContent()
    {
        DrawContent();
    }

    protected abstract void DrawContent();
}


