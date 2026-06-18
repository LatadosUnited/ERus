using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
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
        _isLoadingReleases = true;
        Task.Run(async () => 
        {
            _availableReleases = await _releaseManager.GetAvailableReleasesAsync();
            _isLoadingReleases = false;
        });
    }

    public void Draw()
    {
        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        ImGui.Begin("ERus Hub", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);

        if (ImGui.BeginTabBar("HubTabs"))
        {
            if (ImGui.BeginTabItem("Projects"))
            {
                DrawProjectsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Installs"))
            {
                DrawInstallsTab();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        DrawNewProjectModal();
        DrawAddEngineModal();

        ImGui.End();
    }

    private void DrawProjectsTab()
    {
        ImGui.Spacing();
        if (ImGui.Button("New Project", new Vector2(120, 30)))
        {
            _triggerNewProjectModal = true;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (_config.Projects.Count == 0)
        {
            ImGui.Text("You have no projects yet.");
        }
        else
        {
            foreach (var proj in _config.Projects.ToArray())
            {
                ImGui.PushID(proj.Path);
                ImGui.BeginGroup();
                ImGui.TextDisabled(proj.EngineVersion);
                ImGui.SameLine();
                ImGui.Text(proj.Name);
                ImGui.TextDisabled(proj.Path);
                
                ImGui.SameLine(ImGui.GetWindowWidth() - 100);
                if (ImGui.Button("Open", new Vector2(80, 24)))
                {
                    OpenProject(proj);
                }
                ImGui.EndGroup();
                ImGui.Separator();
                ImGui.PopID();
            }
        }
    }

    private void DrawInstallsTab()
    {
        ImGui.Spacing();
        ImGui.Text("Engine Installation Directory:");
        ImGui.InputText("##InstallDir", ref _installDirectoryPath, 256);
        ImGui.SameLine();
        if (ImGui.Button("..."))
        {
            var dialogResult = NativeFileDialogSharp.Dialog.FolderPicker();
            if (dialogResult.IsOk)
            {
                _installDirectoryPath = dialogResult.Path;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Save Path"))
        {
            _config.DefaultInstallDirectory = _installDirectoryPath;
            ConfigManager.Save(_config);
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
            ImGui.Separator();
            ImGui.PopID();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Available on GitHub");
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
                    if (_downloadProgress >= 0 && _downloadingVersion == release.TagName)
                    {
                        ImGui.ProgressBar(_downloadProgress, new Vector2(ImGui.GetWindowWidth() - 50, 20));
                    }
                    else
                    {
                        // Enable download button only if not currently downloading something else
                        ImGui.BeginDisabled(_downloadProgress >= 0);
                        ImGui.SameLine(ImGui.GetWindowWidth() - 100);
                        if (ImGui.Button("Download", new Vector2(80, 24)))
                        {
                            StartDownload(release);
                        }
                        ImGui.EndDisabled();
                    }
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
                }
                
                var proj = new ProjectData
                {
                    Name = _newProjectName,
                    Path = fullPath,
                    EngineVersion = _selectedEngineVersion,
                    LastModified = DateTime.Now
                };
                
                _config.Projects.Add(proj);
                ConfigManager.Save(_config);
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
                ConfigManager.Save(_config);
                
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

    private void OpenProject(ProjectData project)
    {
        var install = _config.Installs.Find(i => i.VersionName == project.EngineVersion);
        if (install == null || !File.Exists(install.ExecutablePath))
        {
            Console.WriteLine($"[Hub] Erro: Executável da engine não encontrado para a versão {project.EngineVersion}");
            return;
        }

        project.LastModified = DateTime.Now;
        ConfigManager.Save(_config);

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
            Console.WriteLine($"[Hub] Falha ao abrir engine: {ex.Message}");
        }
    }
}
