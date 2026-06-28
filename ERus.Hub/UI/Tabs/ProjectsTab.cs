using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using ERus.Hub.Services;
using ERus.Hub.UI.Modals;

namespace ERus.Hub.UI.Tabs;

public class ProjectsTab
{
    private HubConfig _config;
    private Action<string> _showError;
    private RemoteServerClient _apiClient;

    private SavedServer? _activeServer = null;
    private List<RemoteProject> _remoteProjects = new List<RemoteProject>();
    private bool _isFetchingProjects = false;
    
    // Modals
    private AddServerModal _addServerModal;
    private CreateProjectModal _createProjectModal;
    private ChangeRemoteVersionModal _changeRemoteVersionModal;

    private string _openingProject = "";

    public ProjectsTab(HubConfig config, Action<string> showError)
    {
        _config = config;
        _showError = showError;
        _apiClient = new RemoteServerClient();

        _addServerModal = new AddServerModal(_config, _apiClient, (server) => 
        {
            _activeServer = server;
            FetchProjectsForActiveServer();
        });

        _createProjectModal = new CreateProjectModal(_config, _apiClient, FetchProjectsForActiveServer);
        
        _changeRemoteVersionModal = new ChangeRemoteVersionModal(_config, _apiClient, FetchProjectsForActiveServer);
    }

    public void Draw()
    {
        ImGui.Columns(2, "ProjColumns", true);
        ImGui.SetColumnWidth(0, 250f);

        // Left Column: Servers
        ImGui.Spacing();
        ImGui.Text("Saved Servers");
        ImGui.SameLine(ImGui.GetColumnWidth(0) - 100);
        if (ImGui.Button("+ Add", new Vector2(80, 24)))
        {
            _addServerModal.Open();
        }
        ImGui.Separator();
        
        if (_config.Servers.Count == 0)
        {
            ImGui.TextDisabled("No servers added.");
        }
        else
        {
            foreach (var srv in _config.Servers.ToArray())
            {
                bool isSelected = (_activeServer == srv);
                if (ImGui.Selectable($"{srv.Alias}\n({srv.Ip})", isSelected, ImGuiSelectableFlags.None, new Vector2(0, 40)))
                {
                    _activeServer = srv;
                    FetchProjectsForActiveServer();
                }
                
                if (ImGui.BeginPopupContextItem($"Menu_{srv.Ip}_{srv.Username}"))
                {
                    if (ImGui.Selectable("Remove Server"))
                    {
                        if (_activeServer == srv) _activeServer = null;
                        _config.Servers.Remove(srv);
                        _ = ConfigManager.SaveAsync(_config);
                    }
                    ImGui.EndPopup();
                }
            }
        }

        ImGui.NextColumn();

        // Right Column: Projects
        if (_activeServer == null)
        {
            ImGui.TextDisabled("Select a server to view projects.");
            ImGui.Columns(1);
        }
        else
        {
            DrawRemoteProjects();
        }

        _addServerModal.Draw();
        _createProjectModal.Draw();
        _changeRemoteVersionModal.Draw();
    }

    private void DrawRemoteProjects()
    {
        ImGui.Spacing();
        ImGui.Text($"Projects on {_activeServer!.Alias} ({_activeServer.Username})");
        ImGui.SameLine(ImGui.GetColumnWidth(1) - 130);
        if (ImGui.Button("+ Create Project", new Vector2(120, 24)))
        {
            _createProjectModal.Open(_activeServer);
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (_isFetchingProjects)
        {
            ImGui.TextDisabled("Fetching projects...");
            ImGui.Columns(1);
            return;
        }

        if (_remoteProjects.Count == 0)
        {
            ImGui.TextDisabled("No remote projects found on this server.");
            ImGui.Columns(1);
            return;
        }

        float windowVisibleX2 = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        float cardWidth = 240f;
        float cardHeight = 110f;
        float spacing = ImGui.GetStyle().ItemSpacing.X;

        int i = 0;
        foreach (var proj in _remoteProjects)
        {
            ImGui.PushID(proj.Id);

            if (ImGui.BeginChild($"Card_{i}", new Vector2(cardWidth, cardHeight), ImGuiChildFlags.Border, ImGuiWindowFlags.NoScrollbar))
            {
                Vector2 cursorPos = ImGui.GetCursorPos();
                
                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0, 0));
                if (ImGui.Selectable("##card_select", false, ImGuiSelectableFlags.AllowOverlap, new Vector2(cardWidth, cardHeight)))
                {
                    OpenRemoteProject(proj);
                }
                ImGui.PopStyleVar();

                ImGui.SetCursorPos(cursorPos); // Restore

                ImGui.Text(proj.Name);
                ImGui.TextDisabled(proj.EngineVersion);

                ImGui.Spacing();
                ImGui.TextDisabled(proj.LastModified);

                if (ImGui.BeginPopupContextItem($"Menu_Proj_{proj.Id}"))
                {
                    if (ImGui.Selectable("Edit Version"))
                    {
                        _changeRemoteVersionModal.Open(_activeServer, proj);
                    }
                    ImGui.EndPopup();
                }

                ImGui.EndChild();
            }

            float lastCardMaxX = ImGui.GetItemRectMax().X;
            float nextCardMaxX = lastCardMaxX + spacing + cardWidth;
            if (i < _remoteProjects.Count - 1 && nextCardMaxX < windowVisibleX2)
            {
                ImGui.SameLine();
            }

            ImGui.PopID();
            i++;
        }
        
        ImGui.Columns(1);
    }

    private void FetchProjectsForActiveServer()
    {
        if (_activeServer == null) return;
        _isFetchingProjects = true;
        
        Task.Run(async () =>
        {
            var (success, projects, error) = await _apiClient.FetchProjectsAsync(_activeServer);
            if (success)
            {
                _remoteProjects = projects;
            }
            else
            {
                _showError($"Failed to fetch projects. {error}");
                _activeServer = null;
            }
            _isFetchingProjects = false;
        });
    }

    private void OpenRemoteProject(RemoteProject project)
    {
        var install = _config.Installs.Find(i => i.VersionName == project.EngineVersion);
        if (install == null || !File.Exists(install.ExecutablePath))
        {
            _showError($"Executável da engine não encontrado para a versão {project.EngineVersion}. Instale a engine localmente primeiro.");
            return;
        }

        _openingProject = project.Id;
        Task.Run(async () =>
        {
            try
            {
                string cachePath = string.IsNullOrEmpty(_config.DefaultCacheDirectory) 
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ERusHub", "Cache")
                    : _config.DefaultCacheDirectory;
                string projectCacheDir = Path.Combine(cachePath, "Projects", project.Id);

                var startInfo = new ProcessStartInfo
                {
                    FileName = install.ExecutablePath,
                    Arguments = $"--connect {_activeServer?.Ip} --port 27015 --token {_activeServer?.Token} --remote-project {project.Id} --project \"{projectCacheDir}\"",
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(install.ExecutablePath)
                };
                var proc = Process.Start(startInfo);
                if (proc != null)
                {
                    proc.EnableRaisingEvents = true;
                    proc.Exited += (sender, e) =>
                    {
                        if (proc.ExitCode != 0)
                        {
                            _showError($"A Engine foi encerrada inesperadamente (Exit Code: {proc.ExitCode})");
                        }
                    };
                }
                await Task.Delay(1000); 
            }
            catch (Exception ex)
            {
                _showError($"Falha ao abrir engine: {ex.Message}");
            }
            finally
            {
                _openingProject = "";
            }
        });
    }
}
