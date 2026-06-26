using System;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using ERus.Hub.Services;

namespace ERus.Hub.UI.Modals;

public class CreateProjectModal
{
    private HubConfig _config;
    private RemoteServerClient _apiClient;
    private Action _onProjectCreated;

    public bool IsOpen { get; set; } = false;
    private SavedServer? _activeServer = null;

    private string _createProjectName = "";
    private string _createProjectEngineVersion = "";
    private string _createProjectError = "";
    private bool _isCreatingProject = false;

    public CreateProjectModal(HubConfig config, RemoteServerClient apiClient, Action onProjectCreated)
    {
        _config = config;
        _apiClient = apiClient;
        _onProjectCreated = onProjectCreated;
    }

    public void Open(SavedServer activeServer)
    {
        _activeServer = activeServer;
        _createProjectName = "";
        _createProjectError = "";
        IsOpen = true;

        if (_config.Installs.Count > 0)
        {
            _createProjectEngineVersion = _config.Installs[0].VersionName;
        }
    }

    public void Draw()
    {
        if (!IsOpen) return;
        
        ImGui.OpenPopup("Create Project");

        bool dummy = true;
        if (ImGui.BeginPopupModal("Create Project", ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (!IsOpen)
            {
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }

            ImGui.InputText("Name", ref _createProjectName, 64);
            
            if (ImGui.BeginCombo("Engine Version", _createProjectEngineVersion))
            {
                foreach (var install in _config.Installs)
                {
                    bool isSelected = (_createProjectEngineVersion == install.VersionName);
                    if (ImGui.Selectable(install.VersionName, isSelected))
                    {
                        _createProjectEngineVersion = install.VersionName;
                    }
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();

            if (!string.IsNullOrEmpty(_createProjectError))
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), _createProjectError);
            }

            ImGui.Spacing();
            ImGui.BeginDisabled(_isCreatingProject || string.IsNullOrWhiteSpace(_createProjectEngineVersion));
            if (ImGui.Button("Create", new Vector2(100, 30)))
            {
                AttemptCreateProject();
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(100, 30)))
            {
                IsOpen = false;
            }

            ImGui.EndPopup();
        }
    }

    private void AttemptCreateProject()
    {
        if (_activeServer == null) return;
        _isCreatingProject = true;
        _createProjectError = "";

        Task.Run(async () =>
        {
            var (success, error) = await _apiClient.CreateProjectAsync(_activeServer, _createProjectName, _createProjectEngineVersion);
            if (success)
            {
                _onProjectCreated?.Invoke();
                IsOpen = false;
            }
            else
            {
                _createProjectError = error;
            }
            _isCreatingProject = false;
        });
    }
}
