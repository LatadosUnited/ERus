using System;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using ERus.Hub.Services;

namespace ERus.Hub.UI.Modals;

public class ChangeRemoteVersionModal
{
    private HubConfig _config;
    private RemoteServerClient _apiClient;
    private Action _onProjectUpdated;

    public bool IsOpen { get; set; } = false;
    
    private SavedServer? _activeServer = null;
    private RemoteProject? _projectToEdit = null;

    private string _editRemoteEngineVersion = "";
    private string _editRemoteProjectError = "";
    private bool _isEditingRemoteProject = false;

    public ChangeRemoteVersionModal(HubConfig config, RemoteServerClient apiClient, Action onProjectUpdated)
    {
        _config = config;
        _apiClient = apiClient;
        _onProjectUpdated = onProjectUpdated;
    }

    public void Open(SavedServer activeServer, RemoteProject project)
    {
        _activeServer = activeServer;
        _projectToEdit = project;
        _editRemoteEngineVersion = project.EngineVersion;
        _editRemoteProjectError = "";
        IsOpen = true;
    }

    public void Draw()
    {
        if (!IsOpen) return;
        
        ImGui.OpenPopup("Edit Engine Version");

        bool dummy = true;
        if (ImGui.BeginPopupModal("Edit Engine Version", ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (!IsOpen || _projectToEdit == null)
            {
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }

            ImGui.Text($"Editing: {_projectToEdit.Name}");
            ImGui.Spacing();

            if (ImGui.BeginCombo("Engine Version", _editRemoteEngineVersion))
            {
                foreach (var install in _config.Installs)
                {
                    bool isSelected = (_editRemoteEngineVersion == install.VersionName);
                    if (ImGui.Selectable(install.VersionName, isSelected))
                    {
                        _editRemoteEngineVersion = install.VersionName;
                    }
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();

            if (!string.IsNullOrEmpty(_editRemoteProjectError))
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), _editRemoteProjectError);
            }

            ImGui.Spacing();
            ImGui.BeginDisabled(_isEditingRemoteProject || string.IsNullOrWhiteSpace(_editRemoteEngineVersion));
            if (ImGui.Button("Save", new Vector2(100, 30)))
            {
                AttemptChangeRemoteProjectVersion();
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

    private void AttemptChangeRemoteProjectVersion()
    {
        if (_activeServer == null || _projectToEdit == null) return;
        _isEditingRemoteProject = true;
        _editRemoteProjectError = "";

        Task.Run(async () =>
        {
            var (success, error) = await _apiClient.ChangeProjectVersionAsync(_activeServer, _projectToEdit.Id, _editRemoteEngineVersion);
            if (success)
            {
                _onProjectUpdated?.Invoke();
                IsOpen = false;
            }
            else
            {
                _editRemoteProjectError = error;
            }
            _isEditingRemoteProject = false;
        });
    }
}
