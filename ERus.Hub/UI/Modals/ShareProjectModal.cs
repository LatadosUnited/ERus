using System;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using ERus.Hub.Services;

namespace ERus.Hub.UI.Modals;

public class ShareProjectModal
{
    private HubConfig _config;
    private RemoteServerClient _apiClient;
    private Action _onProjectShared;

    public bool IsOpen { get; set; } = false;
    
    private SavedServer? _activeServer = null;
    private RemoteProject? _projectToShare = null;

    private string _targetUsername = "";
    private string _shareError = "";
    private string _shareSuccess = "";
    private bool _isSharing = false;

    public ShareProjectModal(HubConfig config, RemoteServerClient apiClient, Action onProjectShared)
    {
        _config = config;
        _apiClient = apiClient;
        _onProjectShared = onProjectShared;
    }

    public void Open(SavedServer activeServer, RemoteProject project)
    {
        _activeServer = activeServer;
        _projectToShare = project;
        _targetUsername = "";
        _shareError = "";
        _shareSuccess = "";
        IsOpen = true;
    }

    public void Draw()
    {
        if (!IsOpen) return;
        
        ImGui.OpenPopup("Share Project");

        bool dummy = true;
        if (ImGui.BeginPopupModal("Share Project", ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (!IsOpen || _projectToShare == null)
            {
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }

            ImGui.Text($"Sharing: {_projectToShare.Name}");
            ImGui.Spacing();

            ImGui.InputText("Target Username", ref _targetUsername, 50);

            ImGui.Spacing();

            if (!string.IsNullOrEmpty(_shareError))
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), _shareError);
            }
            if (!string.IsNullOrEmpty(_shareSuccess))
            {
                ImGui.TextColored(new Vector4(0, 1, 0, 1), _shareSuccess);
            }

            ImGui.Spacing();
            ImGui.BeginDisabled(_isSharing || string.IsNullOrWhiteSpace(_targetUsername));
            if (ImGui.Button("Share", new Vector2(100, 30)))
            {
                AttemptShareProject();
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Close", new Vector2(100, 30)))
            {
                IsOpen = false;
            }

            ImGui.EndPopup();
        }
    }

    private void AttemptShareProject()
    {
        if (_activeServer == null || _projectToShare == null) return;
        _isSharing = true;
        _shareError = "";
        _shareSuccess = "";

        Task.Run(async () =>
        {
            var (success, error) = await _apiClient.ShareProjectAsync(_activeServer, _projectToShare.Id, _targetUsername);
            if (success)
            {
                _shareSuccess = "Projeto compartilhado com sucesso!";
                _targetUsername = "";
                _onProjectShared?.Invoke();
            }
            else
            {
                _shareError = error;
            }
            _isSharing = false;
        });
    }
}
