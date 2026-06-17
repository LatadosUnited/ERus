using ImGuiNET;
using System.Numerics;
using ERus.Engine.Scripting;

namespace ERus.Editor.EditorUI.Panels;

/// <summary>
/// Painel de Console do editor.
/// Exibe as mensagens do ConsoleLog com cores por nível:
///   - Branco: Info
///   - Amarelo: Warning
///   - Vermelho: Error
/// Inclui botões para limpar e filtrar por nível.
/// </summary>
public class ConsoleWindow : EditorWindow
{
    private bool _showInfo = true;
    private bool _showWarnings = true;
    private bool _showErrors = true;
    private bool _autoScroll = true;

    public ConsoleWindow() : base("Console") { }

    protected override void DrawContent()
    {
        // --- Toolbar ---
        if (ImGui.Button("Clear"))
        {
            ConsoleLog.Clear();
        }
        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
        ImGui.Checkbox("Info", ref _showInfo);
        ImGui.PopStyleColor();
        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.9f, 0.3f, 1.0f));
        ImGui.Checkbox("Warn", ref _showWarnings);
        ImGui.PopStyleColor();
        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
        ImGui.Checkbox("Error", ref _showErrors);
        ImGui.PopStyleColor();
        ImGui.SameLine();

        ImGui.Checkbox("Auto-Scroll", ref _autoScroll);

        ImGui.Separator();

        // --- Log entries ---
        var entries = ConsoleLog.Entries;

        if (ImGui.BeginChild("ConsoleScrollRegion", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar))
        {
            foreach (var entry in entries)
            {
                // Filtro por nível
                if (entry.Level == ConsoleLog.LogLevel.Info && !_showInfo) continue;
                if (entry.Level == ConsoleLog.LogLevel.Warning && !_showWarnings) continue;
                if (entry.Level == ConsoleLog.LogLevel.Error && !_showErrors) continue;

                // Cor baseada no nível
                var color = entry.Level switch
                {
                    ConsoleLog.LogLevel.Warning => new Vector4(1.0f, 0.9f, 0.3f, 1.0f),
                    ConsoleLog.LogLevel.Error => new Vector4(1.0f, 0.3f, 0.3f, 1.0f),
                    _ => new Vector4(0.85f, 0.85f, 0.85f, 1.0f)
                };

                // Prefixo de nível
                var prefix = entry.Level switch
                {
                    ConsoleLog.LogLevel.Warning => "[WARN] ",
                    ConsoleLog.LogLevel.Error => "[ERR]  ",
                    _ => "[INFO] "
                };

                // Timestamp
                var time = entry.Timestamp.ToString("HH:mm:ss");

                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
                ImGui.Text(time);
                ImGui.PopStyleColor();
                ImGui.SameLine();

                ImGui.PushStyleColor(ImGuiCol.Text, color);
                ImGui.TextWrapped($"{prefix}{entry.Message}");
                ImGui.PopStyleColor();
            }

            if (_autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            {
                ImGui.SetScrollHereY(1.0f);
            }
        }
        ImGui.EndChild();
    }
}


