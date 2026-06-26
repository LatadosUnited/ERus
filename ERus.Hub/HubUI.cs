using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using ImGuiNET;
namespace ERus.Hub;

public class HubUI
{
    private HubConfig _config;
    
    // Add Server Modal State
    private bool _triggerAddServerModal = false;
    private string _loginAlias = "";
    private string _loginIp = "127.0.0.1";
    private string _loginUsername = "";
    private string _loginPassword = "";
    private string _loginError = "";
    private bool _isAuthenticating = false;

    // Create Project Modal State
    private bool _triggerCreateProjectModal = false;
    private bool _closeCreateProjectModal = false;
    private string _createProjectName = "";
    private string _createProjectEngineVersion = "v0.1.0";
    private string _createProjectError = "";
    private bool _isCreatingProject = false;

    // Edit Remote Project State
    private bool _triggerChangeRemoteVersionModal = false;
    private bool _closeChangeRemoteVersionModal = false;
    private RemoteProject? _projectToEditRemoteVersion = null;
    private string _editRemoteEngineVersion = "";
    private string _editRemoteProjectError = "";
    private bool _isEditingRemoteProject = false;
    private SavedServer? _activeServer = null;
    private bool _isFetchingProjects = false;
    private List<RemoteProject> _remoteProjects = new List<RemoteProject>();
    private HttpClient _httpClient = new HttpClient();
    
    // Modal New Project state
    private bool _triggerNewProjectModal = false;
    private string _newProjectName = "NewGame";
    private string _newProjectPath = @"C:\Games";
    private string _selectedEngineVersion = "";
    
    // Modal Add Engine state
    private bool _triggerAddEngineModal = false;
    private string _newEngineVersion = "1.0.0";
    private string _newEnginePath = @"E:\Projetos\ERus\ERus.Editor\bin\Debug\net10.0\ERus.Editor.exe";

    // GitHub Releases state
    private GitHubReleaseManager _releaseManager;
    private List<GitHubRelease> _availableReleases = new List<GitHubRelease>();
    private bool _isLoadingReleases = false;
    private float _downloadProgress = -1f; // -1 indicates not downloading
    private string _downloadingVersion = "";
    private string _errorMessage = "";
    private bool _isFolderPickerOpen = false;
    private string _openingProject = "";
    private DateTime _lastRefreshTime = DateTime.MinValue;
    
    // UI Layout State
    private int _selectedTab = 0; // 0 = Projects, 1 = Installs
    private string _searchQuery = "";

    // Modal Delete Project state
    private ProjectData? _projectToDelete = null;
    private bool _triggerDeleteProjectModal = false;
    
    // Modal Change Engine Version state
    private ProjectData? _projectToEditVersion = null;
    private bool _triggerChangeVersionModal = false;
    private string _editEngineVersion = "";
    
    // Config state
    private string _installDirectoryPath = "";

    public HubUI()
    {
        _config = ConfigManager.Load();
        if (_config.Installs.Count > 0)
        {
            _selectedEngineVersion = _config.Installs[0].VersionName;
        }

        _installDirectoryPath = string.IsNullOrEmpty(_config.DefaultInstallDirectory) 
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ERusHub", "Engines")
            : _config.DefaultInstallDirectory;

        _releaseManager = new GitHubReleaseManager();
        FetchReleases(false);
        CheckDotNetRequirement();
    }

    private void FetchReleases(bool forceRefresh)
    {
        _isLoadingReleases = true;
        Task.Run(async () => 
        {
            _availableReleases = await _releaseManager.GetAvailableReleasesAsync(_config, forceRefresh);
            _isLoadingReleases = false;
        });
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

        float footerHeight = (_downloadProgress >= 0) ? 35f : 0f;
        Vector2 contentSize = new Vector2(0, ImGui.GetContentRegionAvail().Y - footerHeight);

        // Sidebar
        ImGui.BeginChild("Sidebar", new Vector2(220, contentSize.Y), ImGuiChildFlags.Border);
        DrawSidebar();
        ImGui.EndChild();

        ImGui.SameLine();

        // Main Content
        ImGui.BeginChild("MainContent", new Vector2(0, contentSize.Y), ImGuiChildFlags.None);
        if (_selectedTab == 0) DrawProjectsTab();
        else if (_selectedTab == 1) DrawInstallsTab();
        ImGui.EndChild();

        if (footerHeight > 0)
        {
            ImGui.Separator();
            DrawFooter();
        }

        if (_triggerAddServerModal)
        {
            ImGui.OpenPopup("Add Server");
            _triggerAddServerModal = false;
        }
        
        if (_triggerCreateProjectModal)
        {
            ImGui.OpenPopup("Create Project");
            _triggerCreateProjectModal = false;
        }

        if (_triggerChangeRemoteVersionModal)
        {
            ImGui.OpenPopup("Edit Engine Version");
            _triggerChangeRemoteVersionModal = false;
        }

        DrawAddServerModal();
        DrawCreateProjectModal();
        DrawNewProjectModal();
        DrawAddEngineModal();
        DrawDeleteProjectModal();
        DrawChangeVersionModal();
        DrawChangeRemoteVersionModal();

        ImGui.End();
    }

    private void DrawAddServerModal()
    {
        bool dummy = true;
        if (ImGui.BeginPopupModal("Add Server", ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
        {
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
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void DrawCreateProjectModal()
    {
        bool dummy = true;
        if (ImGui.BeginPopupModal("Create Project", ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_closeCreateProjectModal)
            {
                ImGui.CloseCurrentPopup();
                _closeCreateProjectModal = false;
            }

            ImGui.InputText("Name", ref _createProjectName, 64);
            
            // Engine Version Dropdown
            if (ImGui.BeginCombo("Engine Version", _createProjectEngineVersion))
            {
                foreach (var install in _config.Installs)
                {
                    bool isSelected = (_createProjectEngineVersion == install.VersionName);
                    if (ImGui.Selectable(install.VersionName, isSelected))
                    {
                        _createProjectEngineVersion = install.VersionName;
                    }
                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
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
                ImGui.CloseCurrentPopup();
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
            try
            {
                var payload = new { Username = _loginUsername, Password = _loginPassword };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                string endpoint = isRegister ? "/api/register" : "/api/login";
                var response = await _httpClient.PostAsync($"http://{_loginIp}:8080{endpoint}", content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    string token = result.GetProperty("token").GetString() ?? "";
                    
                    var existing = _config.Servers.FirstOrDefault(s => s.Ip == _loginIp && s.Username == _loginUsername);
                    if (existing != null)
                    {
                        existing.Token = token;
                        existing.Alias = _loginAlias;
                        _activeServer = existing;
                    }
                    else
                    {
                        var newServer = new SavedServer
                        {
                            Alias = _loginAlias,
                            Ip = _loginIp,
                            Username = _loginUsername,
                            Token = token
                        };
                        _config.Servers.Add(newServer);
                        _activeServer = newServer;
                    }

                    _ = ConfigManager.SaveAsync(_config);
                    
                    ParseProjectsResponse(result);
                    
                    _triggerAddServerModal = false;
                }
                else
                {
                    try 
                    {
                        var errResult = JsonSerializer.Deserialize<JsonElement>(responseBody);
                        if (errResult.TryGetProperty("error", out var errProp))
                        {
                            _loginError = errProp.GetString() ?? "Unknown error.";
                        }
                        else 
                        {
                            _loginError = isRegister ? "Registration failed." : "Login failed or Invalid Credentials.";
                        }
                    }
                    catch
                    {
                        _loginError = isRegister ? "Registration failed." : "Login failed or Invalid Credentials.";
                    }
                }
            }
            catch (Exception ex)
            {
                _loginError = $"Network Error: {ex.Message}";
            }
            finally
            {
                _isAuthenticating = false;
            }
        });
    }

    private void AttemptCreateProject()
    {
        if (_activeServer == null) return;

        _isCreatingProject = true;
        _createProjectError = "";

        Task.Run(async () =>
        {
            try
            {
                var payload = new { Name = _createProjectName, EngineVersion = _createProjectEngineVersion };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _activeServer.Token);
                var response = await _httpClient.PostAsync($"http://{_activeServer.Ip}:8080/api/projects", content);
                
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _closeCreateProjectModal = true;
                    FetchProjectsForActiveServer(); // Atualiza a lista
                }
                else
                {
                    try 
                    {
                        var errResult = JsonSerializer.Deserialize<JsonElement>(responseBody);
                        if (errResult.TryGetProperty("error", out var errProp))
                        {
                            _createProjectError = errProp.GetString() ?? "Unknown error.";
                        }
                        else 
                        {
                            _createProjectError = "Failed to create project.";
                        }
                    }
                    catch
                    {
                        _createProjectError = "Failed to create project.";
                    }
                }
            }
            catch (Exception ex)
            {
                _createProjectError = $"Network Error: {ex.Message}";
            }
            finally
            {
                _isCreatingProject = false;
            }
        });
    }

    private void AttemptChangeRemoteProjectVersion()
    {
        if (_activeServer == null || _projectToEditRemoteVersion == null) return;

        _isEditingRemoteProject = true;
        _editRemoteProjectError = "";

        Task.Run(async () =>
        {
            try
            {
                var payload = new { Id = _projectToEditRemoteVersion.Id, EngineVersion = _editRemoteEngineVersion };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _activeServer.Token);
                var response = await _httpClient.PutAsync($"http://{_activeServer.Ip}:8080/api/projects", content);
                
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _closeChangeRemoteVersionModal = true;
                    FetchProjectsForActiveServer();
                }
                else
                {
                    try 
                    {
                        var errResult = JsonSerializer.Deserialize<JsonElement>(responseBody);
                        if (errResult.TryGetProperty("error", out var errProp))
                        {
                            _editRemoteProjectError = errProp.GetString() ?? "Unknown error.";
                        }
                        else 
                        {
                            _editRemoteProjectError = "Failed to update project.";
                        }
                    }
                    catch
                    {
                        _editRemoteProjectError = "Failed to update project.";
                    }
                }
            }
            catch (Exception ex)
            {
                _editRemoteProjectError = $"Network Error: {ex.Message}";
            }
            finally
            {
                _isEditingRemoteProject = false;
            }
        });
    }

    private void FetchProjectsForActiveServer()
    {
        if (_activeServer == null) return;
        _isFetchingProjects = true;
        
        Task.Run(async () =>
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _activeServer.Token);
                var response = await _httpClient.GetAsync($"http://{_activeServer.Ip}:8080/api/projects");
                
                if (response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(body);
                    ParseProjectsResponse(result);
                }
                else
                {
                    _errorMessage = $"Failed to fetch projects. Token might be invalid.";
                    _activeServer = null; // Kick out
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Failed to connect to server: {ex.Message}";
                _activeServer = null;
            }
            finally
            {
                _isFetchingProjects = false;
            }
        });
    }

    private void ParseProjectsResponse(JsonElement result)
    {
        _remoteProjects.Clear();
        foreach (var p in result.GetProperty("projects").EnumerateArray())
        {
            _remoteProjects.Add(new RemoteProject
            {
                Id = p.GetProperty("id").GetString() ?? "",
                Name = p.GetProperty("name").GetString() ?? "",
                EngineVersion = p.GetProperty("engineVersion").GetString() ?? "",
                LastModified = p.GetProperty("lastModified").GetString() ?? ""
            });
        }
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
        if (_downloadProgress >= 0)
        {
            ImGui.Text($" Downloading Engine ({_downloadingVersion}): ");
            ImGui.SameLine();
            ImGui.ProgressBar(_downloadProgress, new Vector2(ImGui.GetContentRegionAvail().X - 10, 20));
        }
    }

    private void DrawProjectsTab()
    {
        ImGui.Columns(2, "ProjColumns", true);
        ImGui.SetColumnWidth(0, 250f);

        // Left Column: Servers
        ImGui.Spacing();
        ImGui.Text("Saved Servers");
        ImGui.SameLine(ImGui.GetColumnWidth(0) - 100);
        if (ImGui.Button("+ Add", new Vector2(80, 24)))
        {
            _triggerAddServerModal = true;
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
            return;
        }

        ImGui.Spacing();
        ImGui.Text($"Projects on {_activeServer.Alias} ({_activeServer.Username})");
        ImGui.SameLine(ImGui.GetColumnWidth(1) - 130);
        if (ImGui.Button("+ Create Project", new Vector2(120, 24)))
        {
            _triggerCreateProjectModal = true;
            _createProjectName = "";
            if (_config.Installs.Count > 0)
            {
                _createProjectEngineVersion = _config.Installs[0].VersionName;
            }
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
                        _projectToEditRemoteVersion = proj;
                        _editRemoteEngineVersion = proj.EngineVersion;
                        _triggerChangeRemoteVersionModal = true;
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

    private void DrawInstallsTab()
    {
        ImGui.Spacing();
        ImGui.Text("Engine Installation Directory:");
        ImGui.InputText("##InstallDir", ref _installDirectoryPath, 256);
        ImGui.SameLine();
        ImGui.BeginDisabled(_isFolderPickerOpen);
        if (ImGui.Button("..."))
        {
            _isFolderPickerOpen = true;
            Task.Run(() => 
            {
                var dialogResult = NativeFileDialogSharp.Dialog.FolderPicker();
                if (dialogResult.IsOk)
                {
                    _installDirectoryPath = dialogResult.Path;
                }
                _isFolderPickerOpen = false;
            });
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Save Path"))
        {
            _config.DefaultInstallDirectory = _installDirectoryPath;
            _ = ConfigManager.SaveAsync(_config);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Add Local Engine", new Vector2(150, 30)))
        {
            _triggerAddEngineModal = true;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Installed Locally");
        ImGui.Separator();
        ImGui.Spacing();

        foreach (var inst in _config.Installs.ToArray())
        {
            ImGui.PushID("local_" + inst.ExecutablePath);
            ImGui.Text(inst.VersionName);
            ImGui.TextDisabled(inst.ExecutablePath);
            ImGui.SameLine(ImGui.GetWindowWidth() - 100);
            if (ImGui.Button("Uninstall", new Vector2(80, 24)))
            {
                _config.Installs.Remove(inst);
                _ = ConfigManager.SaveAsync(_config);
                string? parentDir = Path.GetDirectoryName(inst.ExecutablePath);
                if (parentDir != null && parentDir.Contains("ERusHub") && Directory.Exists(parentDir))
                {
                    try { Directory.Delete(parentDir, true); } catch { }
                }
            }
            ImGui.Separator();
            ImGui.PopID();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Available on GitHub");
        ImGui.SameLine(ImGui.GetWindowWidth() - 120);

        var timeSinceRefresh = DateTime.Now - _lastRefreshTime;
        bool canRefresh = timeSinceRefresh.TotalSeconds > 60;

        ImGui.BeginDisabled(!canRefresh || _isLoadingReleases);
        string refreshText = canRefresh ? "Refresh" : $"Wait ({(int)(60 - timeSinceRefresh.TotalSeconds)}s)";
        if (ImGui.Button(refreshText, new Vector2(100, 24)))
        {
            _lastRefreshTime = DateTime.Now;
            FetchReleases(true);
        }
        ImGui.EndDisabled();

        ImGui.Separator();
        ImGui.Spacing();

        if (_isLoadingReleases)
        {
            ImGui.Text("Fetching releases from GitHub...");
        }
        else if (_availableReleases.Count == 0)
        {
            ImGui.TextDisabled("No releases found.");
        }
        else
        {
            foreach (var release in _availableReleases)
            {
                ImGui.PushID("github_" + release.TagName);
                ImGui.Text($"{release.Name} ({release.TagName})");
                ImGui.TextDisabled($"Published at: {release.PublishedAt.ToLocalTime()}");
                
                // Verifica se já está instalada
                bool isInstalled = _config.Installs.Exists(i => i.VersionName == release.TagName);
                
                if (isInstalled)
                {
                    ImGui.SameLine(ImGui.GetWindowWidth() - 100);
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), "Installed");
                }
                else
                {
                    ImGui.BeginDisabled(_downloadProgress >= 0);
                    ImGui.SameLine(ImGui.GetWindowWidth() - 100);
                    if (ImGui.Button("Download", new Vector2(80, 24)))
                    {
                        StartDownload(release);
                    }
                    ImGui.EndDisabled();
                }
                ImGui.Separator();
                ImGui.PopID();
            }
        }
    }

    private void StartDownload(GitHubRelease release)
    {
        if (release.Assets.Count == 0) return;
        
        // Pega o primeiro asset que termine em .zip ou simplesmente o primeiro
        var asset = release.Assets.Find(a => a.Name.EndsWith(".zip")) ?? release.Assets[0];

        _downloadingVersion = release.TagName;
        _downloadProgress = 0f;

        Task.Run(async () =>
        {
            try
            {
                await _releaseManager.DownloadAndInstallAsync(release, asset, _config, (progress) =>
                {
                    _downloadProgress = progress;
                }, (error) => 
                {
                    _errorMessage = error;
                });
            }
            finally
            {
                _downloadProgress = -1f;
                _downloadingVersion = "";
            }
        });
    }

    private void DrawNewProjectModal()
    {
        if (_triggerNewProjectModal)
        {
            ImGui.OpenPopup("Create New Project");
            _triggerNewProjectModal = false;
        }

        bool dummy = true;
        if (ImGui.BeginPopupModal("Create New Project", ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.InputText("Project Name", ref _newProjectName, 64);
            ImGui.InputText("Path", ref _newProjectPath, 256);
            
            if (ImGui.BeginCombo("Engine Version", _selectedEngineVersion))
            {
                foreach (var inst in _config.Installs)
                {
                    bool isSelected = (inst.VersionName == _selectedEngineVersion);
                    if (ImGui.Selectable(inst.VersionName, isSelected))
                    {
                        _selectedEngineVersion = inst.VersionName;
                    }
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();
            if (ImGui.Button("Create", new Vector2(120, 0)))
            {
                string fullPath = Path.Combine(_newProjectPath, _newProjectName);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    Directory.CreateDirectory(Path.Combine(fullPath, "Assets"));
                    Directory.CreateDirectory(Path.Combine(fullPath, "Scripts"));
                    File.WriteAllText(Path.Combine(fullPath, $"{_newProjectName}.erusproj"), "{}");
                }
                
                var proj = new ProjectData
                {
                    Name = _newProjectName,
                    Path = fullPath,
                    EngineVersion = _selectedEngineVersion,
                    LastModified = DateTime.Now
                };
                
                _config.Projects.Add(proj);
                _ = ConfigManager.SaveAsync(_config);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void DrawAddEngineModal()
    {
        if (_triggerAddEngineModal)
        {
            ImGui.OpenPopup("Add Engine Installation");
            _triggerAddEngineModal = false;
        }

        bool dummy = true;
        if (ImGui.BeginPopupModal("Add Engine Installation", ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.InputText("Version Name", ref _newEngineVersion, 32);
            ImGui.InputText("Executable Path", ref _newEnginePath, 256);

            ImGui.Spacing();
            if (ImGui.Button("Add", new Vector2(120, 0)))
            {
                var inst = new EngineInstall
                {
                    VersionName = _newEngineVersion,
                    ExecutablePath = _newEnginePath
                };
                
                _config.Installs.Add(inst);
                _ = ConfigManager.SaveAsync(_config);
                
                if (string.IsNullOrEmpty(_selectedEngineVersion))
                    _selectedEngineVersion = _newEngineVersion;
                    
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void DrawDeleteProjectModal()
    {
        if (_triggerDeleteProjectModal)
        {
            ImGui.OpenPopup("Delete Project");
            _triggerDeleteProjectModal = false;
        }

        bool dummy = true;
        if (ImGui.BeginPopupModal("Delete Project", ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_projectToDelete != null)
            {
                ImGui.Text("Are you sure you want to permanently delete the project:");
                ImGui.TextColored(new Vector4(1, 0.2f, 0.2f, 1), _projectToDelete.Name);
                ImGui.TextDisabled(_projectToDelete.Path);
                ImGui.Spacing();
                ImGui.Text("This action cannot be undone and will delete ALL files from the disk!");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1.0f, 0.4f, 0.4f, 1.0f));
                if (ImGui.Button("Yes, Delete Files", new Vector2(150, 0)))
                {
                    try
                    {
                        if (Directory.Exists(_projectToDelete.Path))
                        {
                            Directory.Delete(_projectToDelete.Path, true);
                        }
                        _config.Projects.Remove(_projectToDelete);
                        _ = ConfigManager.SaveAsync(_config);
                    }
                    catch (Exception ex)
                    {
                        _errorMessage = $"Failed to delete project folder: {ex.Message}";
                    }
                    _projectToDelete = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.PopStyleColor(3);

                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    _projectToDelete = null;
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.EndPopup();
        }
    }

    private void DrawChangeVersionModal()
    {
        if (_triggerChangeVersionModal)
        {
            ImGui.OpenPopup("Change Engine Version");
            _triggerChangeVersionModal = false;
        }

        bool dummy = true;
        if (ImGui.BeginPopupModal("Change Engine Version", ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_projectToEditVersion != null)
            {
                ImGui.Text($"Change Engine Version for: {_projectToEditVersion.Name}");
                ImGui.Spacing();
                
                if (ImGui.BeginCombo("Engine Version", _editEngineVersion))
                {
                    foreach (var inst in _config.Installs)
                    {
                        bool isSelected = (inst.VersionName == _editEngineVersion);
                        if (ImGui.Selectable(inst.VersionName, isSelected))
                        {
                            _editEngineVersion = inst.VersionName;
                        }
                        if (isSelected) ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (ImGui.Button("Save", new Vector2(120, 0)))
                {
                    _projectToEditVersion.EngineVersion = _editEngineVersion;
                    _projectToEditVersion.LastModified = DateTime.Now;
                    _ = ConfigManager.SaveAsync(_config);
                    
                    _projectToEditVersion = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    _projectToEditVersion = null;
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.EndPopup();
        }
    }

    private void DrawChangeRemoteVersionModal()
    {
        bool dummy = true;
        if (ImGui.BeginPopupModal("Edit Engine Version", ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_closeChangeRemoteVersionModal)
            {
                ImGui.CloseCurrentPopup();
                _closeChangeRemoteVersionModal = false;
            }

            if (_projectToEditRemoteVersion != null)
            {
                ImGui.Text($"Editing: {_projectToEditRemoteVersion.Name}");
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
                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndPopup();
        }
    }

    private void OpenRemoteProject(RemoteProject project)
    {
        var install = _config.Installs.Find(i => i.VersionName == project.EngineVersion);
        if (install == null || !File.Exists(install.ExecutablePath))
        {
            _errorMessage = $"Executável da engine não encontrado para a versão {project.EngineVersion}. Instale a engine localmente primeiro.";
            return;
        }

        _openingProject = project.Id;
        Task.Run(async () =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = install.ExecutablePath,
                    Arguments = $"--connect {_activeServer?.Ip} --port 27015 --token {_activeServer?.Token} --remote-project {project.Id}",
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
                            _errorMessage = $"A Engine foi encerrada inesperadamente (Exit Code: {proc.ExitCode})";
                        }
                    };
                }
                await Task.Delay(1000); 
            }
            catch (Exception ex)
            {
                _errorMessage = $"Falha ao abrir engine: {ex.Message}";
            }
            finally
            {
                _openingProject = "";
            }
        });
    }
}
