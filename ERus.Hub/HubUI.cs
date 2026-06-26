using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using ERus.Hub.UI.Tabs;

namespace ERus.Hub;

public class HubUI
{
    private HubConfig _config;
    
    // Sub-components
    private ProjectsTab _projectsTab;
    private InstallsTab _installsTab;
    
    private string _errorMessage = "";
    
    // UI Layout State
    private int _selectedTab = 0; // 0 = Projects, 1 = Installs

    public HubUI()
    {
        _config = ConfigManager.Load();

        Action<string> showError = (msg) => _errorMessage = msg;

        _projectsTab = new ProjectsTab(_config, showError);
        _installsTab = new InstallsTab(_config, showError);

        CheckDotNetRequirement();
    }

    private void CheckDotNetRequirement()
    {
        Task.Run(() => 
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--list-runtimes",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    if (!output.Contains("Microsoft.NETCore.App 10.0"))
                    {
                        _errorMessage = "Atenção: O Runtime do .NET 10.0 não foi encontrado. A Engine poderá falhar ao iniciar.";
                    }
                }
            }
            catch
            {
                _errorMessage = "Atenção: 'dotnet' não reconhecido. O SDK/Runtime do .NET pode não estar instalado.";
            }
        });
    }

    public void Draw()
    {
        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        ImGui.Begin("ERus Hub", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);

        if (!string.IsNullOrEmpty(_errorMessage))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.2f, 0.2f, 1));
            ImGui.TextWrapped($"Error: {_errorMessage}");
            ImGui.PopStyleColor();
            if (ImGui.Button("Dismiss")) _errorMessage = "";
            ImGui.Separator();
        }

        float footerHeight = (_installsTab.DownloadProgress >= 0) ? 35f : 0f;
        Vector2 contentSize = new Vector2(0, ImGui.GetContentRegionAvail().Y - footerHeight);

        // Sidebar
        ImGui.BeginChild("Sidebar", new Vector2(220, contentSize.Y), ImGuiChildFlags.Border);
        DrawSidebar();
        ImGui.EndChild();

        ImGui.SameLine();

        // Main Content
        ImGui.BeginChild("MainContent", new Vector2(0, contentSize.Y), ImGuiChildFlags.None);
        if (_selectedTab == 0) 
        {
            _projectsTab.Draw();
        }
        else if (_selectedTab == 1) 
        {
            _installsTab.Draw();
        }
        ImGui.EndChild();

        if (footerHeight > 0)
        {
            ImGui.Separator();
            DrawFooter();
        }

        ImGui.End();
    }

    private void DrawSidebar()
    {
        ImGui.Spacing();
        ImGui.TextDisabled(" ERus Engine");
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Selectable(" Projects", _selectedTab == 0, ImGuiSelectableFlags.None, new Vector2(0, 30))) _selectedTab = 0;
        ImGui.Spacing();
        if (ImGui.Selectable(" Installs", _selectedTab == 1, ImGuiSelectableFlags.None, new Vector2(0, 30))) _selectedTab = 1;
    }

    private void DrawFooter()
    {
        if (_installsTab.DownloadProgress >= 0)
        {
            ImGui.Text($" Downloading Engine ({_installsTab.DownloadingVersion}): ");
            ImGui.SameLine();
            ImGui.ProgressBar(_installsTab.DownloadProgress, new Vector2(ImGui.GetContentRegionAvail().X - 10, 20));
        }
    }
}
