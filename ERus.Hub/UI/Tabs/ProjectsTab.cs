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

    private bool _showingLocalProjects = true;
    private SavedServer? _activeServer = null;
    private List<RemoteProject> _remoteProjects = new List<RemoteProject>();
    private bool _isFetchingProjects = false;
    
    // Modals
    private AddServerModal _addServerModal;
    private CreateProjectModal _createProjectModal;
    private ChangeRemoteVersionModal _changeRemoteVersionModal;
    private ShareProjectModal _shareProjectModal;

    private string _openingProject = "";

    public ProjectsTab(HubConfig config, Action<string> showError)
    {
        _config = config;
        _showError = showError;
        _apiClient = new RemoteServerClient();

        _addServerModal = new AddServerModal(_config, _apiClient, (server) => 
        {
            _activeServer = server;
            _showingLocalProjects = false;
            FetchProjectsForActiveServer();
        });

        _createProjectModal = new CreateProjectModal(_config, _apiClient, FetchProjectsForActiveServer);
        _changeRemoteVersionModal = new ChangeRemoteVersionModal(_config, _apiClient, FetchProjectsForActiveServer);
        _shareProjectModal = new ShareProjectModal(_config, _apiClient, () => 
        {
            if (_activeServer != null) FetchProjectsForActiveServer();
        });
    }

    public void Draw()
    {
        ImGui.Columns(2, "ProjColumns", true);
        ImGui.SetColumnWidth(0, 250f);

        // Left Column: Navigation (Local & Servers)
        ImGui.Spacing();
        
        bool localSelected = _showingLocalProjects;
        if (ImGui.Selectable(" Local Projects\n (This Machine)", localSelected, ImGuiSelectableFlags.None, new Vector2(0, 40)))
        {
            _showingLocalProjects = true;
            _activeServer = null;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Remote Servers");
        ImGui.SameLine(ImGui.GetColumnWidth(0) - 100);
        if (ImGui.Button("+ Add", new Vector2(80, 24)))
        {
            _addServerModal.Open();
        }
        ImGui.Separator();
        
        if (_config.Servers.Count == 0)
        {
            ImGui.TextDisabled("No remote servers.");
        }
        else
        {
            foreach (var srv in _config.Servers.ToArray())
            {
                bool isSelected = (!_showingLocalProjects && _activeServer == srv);
                if (ImGui.Selectable($"{srv.Alias}\n({srv.Ip})", isSelected, ImGuiSelectableFlags.None, new Vector2(0, 40)))
                {
                    _showingLocalProjects = false;
                    _activeServer = srv;
                    FetchProjectsForActiveServer();
                }
                
                if (ImGui.BeginPopupContextItem($"Menu_{srv.Ip}_{srv.Username}"))
                {
                    if (ImGui.Selectable("Remove Server"))
                    {
                        if (_activeServer == srv) 
                        {
                            _activeServer = null;
                            _showingLocalProjects = true;
                        }
                        _config.Servers.Remove(srv);
                        _ = ConfigManager.SaveAsync(_config);
                    }
                    ImGui.EndPopup();
                }
            }
        }

        ImGui.NextColumn();

        // Right Column: Projects View
        if (_showingLocalProjects)
        {
            DrawLocalProjects();
        }
        else if (_activeServer != null)
        {
            DrawRemoteProjects();
        }
        else
        {
            ImGui.TextDisabled("Select a server or local projects.");
            ImGui.Columns(1);
        }

        _addServerModal.Draw();
        _createProjectModal.Draw();
        _changeRemoteVersionModal.Draw();
        _shareProjectModal.Draw();
    }

    private void DrawLocalProjects()
    {
        ImGui.Spacing();
        ImGui.Text("Local Projects");
        ImGui.SameLine(ImGui.GetColumnWidth(1) - 240);
        
        if (ImGui.Button("+ New Local", new Vector2(110, 24)))
        {
            CreateDefaultLocalProject();
        }
        ImGui.SameLine();
        if (ImGui.Button("+ Add from Disk", new Vector2(120, 24)))
        {
            AddLocalProjectFromDisk();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (_config.Projects.Count == 0)
        {
            ImGui.TextDisabled("Nenhum projeto local encontrado. Clique em '+ New Local' para criar.");
            ImGui.Columns(1);
            return;
        }

        float windowVisibleX2 = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        float cardWidth = 240f;
        float cardHeight = 110f;
        float spacing = ImGui.GetStyle().ItemSpacing.X;

        int i = 0;
        foreach (var proj in _config.Projects.ToArray())
        {
            ImGui.PushID($"local_{i}");

            if (ImGui.BeginChild($"LocalCard_{i}", new Vector2(cardWidth, cardHeight), ImGuiChildFlags.Border, ImGuiWindowFlags.NoScrollbar))
            {
                Vector2 cursorPos = ImGui.GetCursorPos();
                
                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0, 0));
                if (ImGui.Selectable("##local_card_select", false, ImGuiSelectableFlags.AllowOverlap, new Vector2(cardWidth, cardHeight)))
                {
                    OpenLocalProject(proj);
                }
                ImGui.PopStyleVar();

                ImGui.SetCursorPos(cursorPos);

                ImGui.Text(proj.Name);
                ImGui.TextDisabled(proj.EngineVersion);

                ImGui.Spacing();
                string displayPath = proj.Path.Length > 28 ? "..." + proj.Path.Substring(proj.Path.Length - 25) : proj.Path;
                ImGui.TextDisabled(displayPath);

                if (ImGui.BeginPopupContextItem($"LocalMenu_{i}"))
                {
                    if (ImGui.Selectable("Open in File Explorer"))
                    {
                        if (Directory.Exists(proj.Path)) Process.Start("explorer.exe", proj.Path);
                    }
                    if (ImGui.Selectable("Publish & Share to Server..."))
                    {
                        _shareProjectModal.OpenForLocalProject(proj);
                    }
                    ImGui.Separator();
                    if (ImGui.Selectable("Remove from List"))
                    {
                        _config.Projects.Remove(proj);
                        _ = ConfigManager.SaveAsync(_config);
                    }
                    ImGui.EndPopup();
                }

                ImGui.EndChild();
            }

            float lastCardMaxX = ImGui.GetItemRectMax().X;
            float nextCardMaxX = lastCardMaxX + spacing + cardWidth;
            if (i < _config.Projects.Count - 1 && nextCardMaxX < windowVisibleX2)
            {
                ImGui.SameLine();
            }

            ImGui.PopID();
            i++;
        }
        
        ImGui.Columns(1);
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

                ImGui.SetCursorPos(cursorPos);

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
                    if (ImGui.Selectable("Share with Teammate..."))
                    {
                        _shareProjectModal.Open(_activeServer, proj);
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

    private void CreateDefaultLocalProject()
    {
        try
        {
            string defaultVer = _config.Installs.Count > 0 ? _config.Installs[0].VersionName : "v0.5.20";
            string baseDir = string.IsNullOrEmpty(_config.DefaultCacheDirectory) 
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ERusProjects")
                : Path.Combine(_config.DefaultCacheDirectory, "Projects");

            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

            string projName = $"NewProject_{DateTime.Now:yyyyMMdd_HHmmss}";
            string projPath = Path.Combine(baseDir, projName);
            Directory.CreateDirectory(projPath);
            Directory.CreateDirectory(Path.Combine(projPath, "Assets"));
            Directory.CreateDirectory(Path.Combine(projPath, "Assets", "Scripts"));

            var newProj = new ProjectData
            {
                Name = projName,
                Path = projPath,
                EngineVersion = defaultVer,
                LastModified = DateTime.Now
            };

            _config.Projects.Add(newProj);
            _ = ConfigManager.SaveAsync(_config);
        }
        catch (Exception ex)
        {
            _showError($"Erro ao criar projeto local: {ex.Message}");
        }
    }

    private void AddLocalProjectFromDisk()
    {
        try
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var newProj = new ProjectData
            {
                Name = "ImportedProject",
                Path = baseDir,
                EngineVersion = _config.Installs.Count > 0 ? _config.Installs[0].VersionName : "v0.5.20",
                LastModified = DateTime.Now
            };
            _config.Projects.Add(newProj);
            _ = ConfigManager.SaveAsync(_config);
        }
        catch (Exception ex)
        {
            _showError($"Erro ao importar projeto: {ex.Message}");
        }
    }

    private void OpenLocalProject(ProjectData project)
    {
        var install = _config.Installs.Find(i => i.VersionName == project.EngineVersion);
        if (install == null || !File.Exists(install.ExecutablePath))
        {
            _showError($"Executável da engine não encontrado para a versão {project.EngineVersion}. Instale a engine localmente primeiro.");
            return;
        }

        Task.Run(() =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = install.ExecutablePath,
                    Arguments = $"--project \"{project.Path}\"",
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(install.ExecutablePath)
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                _showError($"Falha ao abrir projeto local: {ex.Message}");
            }
        });
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
