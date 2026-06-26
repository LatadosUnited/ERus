using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using ERus.Hub.Services;

namespace ERus.Hub.UI.Modals;

public class AddServerModal
{
    private HubConfig _config;
    private RemoteServerClient _apiClient;
    private Action<SavedServer> _onServerAdded;

    public bool IsOpen { get; set; } = false;

    private string _loginAlias = "";
    private string _loginIp = "127.0.0.1";
    private string _loginUsername = "";
    private string _loginPassword = "";
    private string _loginError = "";
    private bool _isAuthenticating = false;

    public AddServerModal(HubConfig config, RemoteServerClient apiClient, Action<SavedServer> onServerAdded)
    {
        _config = config;
        _apiClient = apiClient;
        _onServerAdded = onServerAdded;
    }

    public void Open()
    {
        IsOpen = true;
        _loginError = "";
    }

    public void Draw()
    {
        if (!IsOpen) return;
        
        ImGui.OpenPopup("Add Server");

        bool dummy = true;
        if (ImGui.BeginPopupModal("Add Server", ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (!IsOpen)
            {
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }

            ImGui.InputText("Alias", ref _loginAlias, 64);
            ImGui.InputText("Server IP", ref _loginIp, 64);
            ImGui.InputText("Username", ref _loginUsername, 64);
            ImGui.InputText("Password", ref _loginPassword, 64, ImGuiInputTextFlags.Password);

            ImGui.Spacing();

            if (!string.IsNullOrEmpty(_loginError))
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), _loginError);
            }

            ImGui.Spacing();
            ImGui.BeginDisabled(_isAuthenticating);
            if (ImGui.Button("Connect & Save", new Vector2(150, 30)))
            {
                AttemptAuth(false);
            }
            ImGui.SameLine();
            if (ImGui.Button("Register", new Vector2(100, 30)))
            {
                AttemptAuth(true);
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

    private void AttemptAuth(bool isRegister)
    {
        _isAuthenticating = true;
        _loginError = "";

        Task.Run(async () =>
        {
            var (success, token, error) = await _apiClient.AuthenticateAsync(_loginIp, _loginUsername, _loginPassword, isRegister);
            if (success)
            {
                var existing = _config.Servers.FirstOrDefault(s => s.Ip == _loginIp && s.Username == _loginUsername);
                SavedServer server;
                if (existing != null)
                {
                    existing.Token = token;
                    existing.Alias = _loginAlias;
                    server = existing;
                }
                else
                {
                    server = new SavedServer
                    {
                        Alias = _loginAlias,
                        Ip = _loginIp,
                        Username = _loginUsername,
                        Token = token
                    };
                    _config.Servers.Add(server);
                }

                _ = ConfigManager.SaveAsync(_config);
                _onServerAdded?.Invoke(server);
                IsOpen = false;
            }
            else
            {
                _loginError = error;
            }
            _isAuthenticating = false;
        });
    }
}
