using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using ERus.Hub.Services;

namespace ERus.Hub.UI.Tabs;

public class InstallsTab
{
    private HubConfig _config;
    private Action<string> _showError;
    private GitHubReleaseManager _releaseManager;

    private string _installDirectoryPath = "";
    private string _cacheDirectoryPath = "";
    private bool _isFolderPickerOpen = false;

    // Modal Add Engine state
    private bool _triggerAddEngineModal = false;
    private string _newEngineVersion = "1.0.0";
    private string _newEnginePath = @"E:\Projetos\ERus\ERus.Editor\bin\Debug\net10.0\ERus.Editor.exe";

    // GitHub Releases state
    private List<GitHubRelease> _availableReleases = new List<GitHubRelease>();
    private bool _isLoadingReleases = false;
    public float DownloadProgress { get; private set; } = -1f; // -1 indicates not downloading
    public string DownloadingVersion { get; private set; } = "";
    private DateTime _lastRefreshTime = DateTime.MinValue;
    private string _selectedEngineVersion = ""; // Optional if we want to store it here too

    public InstallsTab(HubConfig config, Action<string> showError)
    {
        _config = config;
        _showError = showError;
        _releaseManager = new GitHubReleaseManager();

        _installDirectoryPath = string.IsNullOrEmpty(_config.DefaultInstallDirectory) 
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ERusHub", "Engines")
            : _config.DefaultInstallDirectory;

        _cacheDirectoryPath = string.IsNullOrEmpty(_config.DefaultCacheDirectory) 
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ERusHub", "Cache")
            : _config.DefaultCacheDirectory;

        FetchReleases(false);
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

    public void Draw()
    {
        ImGui.Spacing();
        ImGui.Text("Engine Installation Directory:");
        ImGui.InputText("##InstallDir", ref _installDirectoryPath, 256);
        ImGui.SameLine();
        ImGui.BeginDisabled(_isFolderPickerOpen);
        if (ImGui.Button("...##BtnInstall"))
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
        if (ImGui.Button("Save Path##BtnInstallSave"))
        {
            _config.DefaultInstallDirectory = _installDirectoryPath;
            _ = ConfigManager.SaveAsync(_config);
        }

        ImGui.Spacing();
        ImGui.Text("Remote Projects Cache Directory:");
        ImGui.InputText("##CacheDir", ref _cacheDirectoryPath, 256);
        ImGui.SameLine();
        ImGui.BeginDisabled(_isFolderPickerOpen);
        if (ImGui.Button("...##BtnCache"))
        {
            _isFolderPickerOpen = true;
            Task.Run(() => 
            {
                var dialogResult = NativeFileDialogSharp.Dialog.FolderPicker();
                if (dialogResult.IsOk)
                {
                    _cacheDirectoryPath = dialogResult.Path;
                }
                _isFolderPickerOpen = false;
            });
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Save Path##BtnCacheSave"))
        {
            _config.DefaultCacheDirectory = _cacheDirectoryPath;
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
                    ImGui.BeginDisabled(DownloadProgress >= 0);
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

        DrawAddEngineModal();
    }

    private void StartDownload(GitHubRelease release)
    {
        if (release.Assets.Count == 0) return;
        
        var asset = release.Assets.Find(a => a.Name.EndsWith(".zip")) ?? release.Assets[0];

        DownloadingVersion = release.TagName;
        DownloadProgress = 0f;

        Task.Run(async () =>
        {
            try
            {
                await _releaseManager.DownloadAndInstallAsync(release, asset, _config, (progress) =>
                {
                    DownloadProgress = progress;
                }, (error) => 
                {
                    _showError(error);
                });
            }
            finally
            {
                DownloadProgress = -1f;
                DownloadingVersion = "";
            }
        });
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
}
