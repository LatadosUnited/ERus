namespace ERus.Editor.EditorUI.Managers;

public static class DragDropState
{
    /// <summary>
    /// Holds the currently dragged payload data as a string to avoid unsafe memory allocations with ImGui payloads.
    /// </summary>
    public static string DraggedPayload { get; set; } = string.Empty;
}
