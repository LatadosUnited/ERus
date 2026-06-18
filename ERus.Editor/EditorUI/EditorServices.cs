using ERus.Editor.EditorUI.Managers;

namespace ERus.Editor.EditorUI;

public static class EditorServices
{
    public static SelectionManager Selection { get; } = new SelectionManager();
}
