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
    private RemoteProject? _remoteProjectToShare = null;
    private ProjectData? _localProjectToPublish = null;

    private int _selectedServerIndex = 0;
    private string _targetUsername = "";
    private string _shareError = "";
    private string _shareSuccess = "";
    private bool _isProcessing = false;

    public ShareProjectModal(HubConfig config, RemoteServerClient apiClient, Action onProjectShared)
    {
        _config = config;
        _apiClient = apiClient;
        _onProjectShared = onProjectShared;
    }

    public void Open(SavedServer activeServer, RemoteProject project)
    {
        _activeServer = activeServer;
        _remoteProjectToShare = project;
        _localProjectToPublish = null;
        _targetUsername = "";
        _shareError = "";
        _shareSuccess = "";
        IsOpen = true;
    }

    public void OpenForLocalProject(ProjectData localProject)
    {
        _localProjectToPublish = localProject;
        _remoteProjectToShare = null;
        _selectedServerIndex = 0;
        _targetUsername = "";
        _shareError = "";
        _shareSuccess = "";
        IsOpen = true;

        if (_config.Servers.Count > 0)
        {
            _activeServer = _config.Servers[0];
        }
        else
        {
            _activeServer = null;
        }
    }

    public void Draw()
    {
        if (!IsOpen) return;
        
        string title = _localProjectToPublish != null ? "Publish & Share Local Project" : "Share Remote Project";
        ImGui.OpenPopup(title);

        bool dummy = true;
        if (ImGui.BeginPopupModal(title, ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (!IsOpen || (_remoteProjectToShare == null && _localProjectToPublish == null))
            {
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }

            if (_localProjectToPublish != null)
            {
                // Modo: Publicar Projeto Local no Servidor
                ImGui.Text($"Local Project: {_localProjectToPublish.Name}");
                ImGui.TextDisabled($"Engine Version: {_localProjectToPublish.EngineVersion}");
                ImGui.Spacing();

                if (_config.Servers.Count == 0)
                {
                    ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Nenhum servidor remoto cadastrado. Adicione um servidor primeiro.");
                }
                else
                {
                    string currentServerName = _activeServer != null ? $"{_activeServer.Alias} ({_activeServer.Username})" : "Select Server...";
                    if (ImGui.BeginCombo("Target Server", currentServerName))
                    {
                        for (int s = 0; s < _config.Servers.Count; s++)
                        {
                            var srv = _config.Servers[s];
                            bool isSelected = (_activeServer == srv);
                            if (ImGui.Selectable($"{srv.Alias} ({srv.Ip})", isSelected))
                            {
                                _activeServer = srv;
                                _selectedServerIndex = s;
                            }
                            if (isSelected) ImGui.SetItemDefaultFocus();
                        }
                        ImGui.EndCombo();
                    }

                    ImGui.Spacing();
                    ImGui.InputText("Collaborator Username (Optional)", ref _targetUsername, 50);
                }
            }
            else if (_remoteProjectToShare != null)
            {
                // Modo: Compartilhar Projeto Remoto Existente
                ImGui.Text($"Project: {_remoteProjectToShare.Name}");
                ImGui.TextDisabled($"Server: {_activeServer?.Alias} ({_activeServer?.Username})");
                ImGui.Spacing();

                ImGui.InputText("Collaborator Username", ref _targetUsername, 50);
            }

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
            
            bool canSubmit = !_isProcessing && _activeServer != null;
            if (_remoteProjectToShare != null && string.IsNullOrWhiteSpace(_targetUsername))
            {
                canSubmit = false;
            }

            ImGui.BeginDisabled(!canSubmit);
            string buttonText = _localProjectToPublish != null ? "Publish & Share" : "Share";
            if (ImGui.Button(buttonText, new Vector2(130, 30)))
            {
                if (_localProjectToPublish != null)
                {
                    AttemptPublishAndShareLocalProject();
                }
                else
                {
                    AttemptShareRemoteProject();
                }
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

    private void AttemptShareRemoteProject()
    {
        if (_activeServer == null || _remoteProjectToShare == null) return;
        _isProcessing = true;
        _shareError = "";
        _shareSuccess = "";

        Task.Run(async () =>
        {
            var (success, error) = await _apiClient.ShareProjectAsync(_activeServer, _remoteProjectToShare.Id, _targetUsername);
            if (success)
            {
                _shareSuccess = $"Projeto compartilhado com '{_targetUsername}' com sucesso!";
                _targetUsername = "";
                _onProjectShared?.Invoke();
            }
            else
            {
                _shareError = error;
            }
            _isProcessing = false;
        });
    }

    private void AttemptPublishAndShareLocalProject()
    {
        if (_activeServer == null || _localProjectToPublish == null) return;
        _isProcessing = true;
        _shareError = "";
        _shareSuccess = "";

        Task.Run(async () =>
        {
            // 1. Criar projeto no servidor
            var (success, error) = await _apiClient.CreateProjectAsync(_activeServer, _localProjectToPublish.Name, _localProjectToPublish.EngineVersion);
            if (!success)
            {
                _shareError = $"Falha ao publicar projeto: {error}";
                _isProcessing = false;
                return;
            }

            // 2. Se informou colaborador, compartilha imediatamente
            if (!string.IsNullOrWhiteSpace(_targetUsername))
            {
                // Buscar lista atualizada para pegar o ID do projeto criado
                var (fetchOk, projects, fetchErr) = await _apiClient.FetchProjectsAsync(_activeServer);
                if (fetchOk)
                {
                    var created = projects.Find(p => p.Name == _localProjectToPublish.Name);
                    if (created != null)
                    {
                        var (shareOk, shareErr) = await _apiClient.ShareProjectAsync(_activeServer, created.Id, _targetUsername);
                        if (shareOk)
                        {
                            _shareSuccess = $"Projeto publicado e compartilhado com '{_targetUsername}' com sucesso!";
                        }
                        else
                        {
                            _shareSuccess = $"Projeto publicado! Falha ao adicionar colaborador: {shareErr}";
                        }
                    }
                    else
                    {
                        _shareSuccess = "Projeto publicado no servidor com sucesso!";
                    }
                }
                else
                {
                    _shareSuccess = "Projeto publicado no servidor com sucesso!";
                }
            }
            else
            {
                _shareSuccess = "Projeto publicado no servidor com sucesso!";
            }

            _onProjectShared?.Invoke();
            _isProcessing = false;
        });
    }
}
