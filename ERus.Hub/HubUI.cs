using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using System.Linq;
using ImGuiNET;

namespace ERus.Hub;

public class HubUI
{
    private HubConfig _config;
    
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

        DrawNewProjectModal();
        DrawAddEngineModal();
        DrawDeleteProjectModal();

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
        if (_downloadProgress >= 0)
        {
            ImGui.Text($" Downloading Engine ({_downloadingVersion}): ");
            ImGui.SameLine();
            ImGui.ProgressBar(_downloadProgress, new Vector2(ImGui.GetContentRegionAvail().X - 10, 20));
        }
    }

    private void DrawProjectsTab()
    {
        ImGui.Spacing();
        ImGui.InputTextWithHint("##Search", "Search projects...", ref _searchQuery, 128);
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 120);
        if (ImGui.Button("New Project", new Vector2(120, 26)))
        {
            _triggerNewProjectModal = true;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (_config.Projects.Count == 0)
        {
            ImGui.TextDisabled("You have no projects yet.");
            return;
        }

        var sortedProjects = _config.Projects.OrderByDescending(p => p.LastModified).ToArray();
        
        float windowVisibleX2 = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        float cardWidth = 240f;
        float cardHeight = 110f;
        float spacing = ImGui.GetStyle().ItemSpacing.X;

        int i = 0;
        foreach (var proj in sortedProjects)
        {
            if (!string.IsNullOrEmpty(_searchQuery) && !proj.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
                continue;

            ImGui.PushID(proj.Path);

            bool exists = Directory.Exists(proj.Path);
            
            if (ImGui.BeginChild($"Card_{i}", new Vector2(cardWidth, cardHeight), ImGuiChildFlags.Border, ImGuiWindowFlags.NoScrollbar))
            {
                Vector2 cursorPos = ImGui.GetCursorPos();
                
                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0, 0));
                if (ImGui.Selectable("##card_select", false, ImGuiSelectableFlags.None, new Vector2(cardWidth, cardHeight)) && exists)
                {
                    OpenProject(proj);
                }
                ImGui.PopStyleVar();

                ImGui.SetCursorPos(cursorPos); // Restore

                if (!exists) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.3f, 0.3f, 1));
                
                ImGui.Text(proj.Name);
                
                if (!exists)
                {
                    ImGui.Text("(Not found)");
                    ImGui.PopStyleColor();
                }
                else
                {
                    ImGui.TextDisabled(proj.EngineVersion);
                }

                ImGui.Spacing();
                ImGui.TextDisabled(proj.LastModified.ToString("yyyy-MM-dd HH:mm"));

                ImGui.SetCursorPos(new Vector2(cardWidth - 35, 5));
                if (ImGui.Button("..."))
                {
                    ImGui.OpenPopup("ProjMenu");
                }

                if (ImGui.BeginPopup("ProjMenu"))
                {
                    if (ImGui.Selectable("Open") && exists) OpenProject(proj);
                    if (ImGui.Selectable("Remove from List"))
                    {
                        _config.Projects.Remove(proj);
                        _ = ConfigManager.SaveAsync(_config);
                    }
                    if (ImGui.Selectable("Delete from Disk"))
                    {
                        _projectToDelete = proj;
                        _triggerDeleteProjectModal = true;
                    }
                    ImGui.EndPopup();
                }

                ImGui.EndChild();
            }

            float lastCardMaxX = ImGui.GetItemRectMax().X;
            float nextCardMaxX = lastCardMaxX + spacing + cardWidth;
            if (i < sortedProjects.Length - 1 && nextCardMaxX < windowVisibleX2)
            {
                ImGui.SameLine();
            }

            ImGui.PopID();
            i++;
        }
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

    private void OpenProject(ProjectData project)
    {
        var install = _config.Installs.Find(i => i.VersionName == project.EngineVersion);
        if (install == null || !File.Exists(install.ExecutablePath))
        {
            _errorMessage = $"Executável da engine não encontrado para a versão {project.EngineVersion}";
            return;
        }

        project.LastModified = DateTime.Now;
        _ = ConfigManager.SaveAsync(_config);

        _openingProject = project.Path;
        Task.Run(async () =>
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
                await Task.Delay(1000); // Dar um feedback visual rápido
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
