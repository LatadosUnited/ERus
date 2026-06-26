using System;
using System.Collections.Generic;

namespace ERus.Hub;

public class ProjectData
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.Now;
}

public class RemoteProject
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string EngineVersion { get; set; }
    public string LastModified { get; set; }
}

public class EngineInstall
{
    public string VersionName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
}

public class SavedServer
{
    public string Alias { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class HubConfig
{
    public string DefaultInstallDirectory { get; set; } = string.Empty;
    public List<ProjectData> Projects { get; set; } = new List<ProjectData>();
    public List<EngineInstall> Installs { get; set; } = new List<EngineInstall>();
    public List<SavedServer> Servers { get; set; } = new List<SavedServer>();
    public DateTime LastGitHubCheck { get; set; } = DateTime.MinValue;
    public List<GitHubRelease> CachedReleases { get; set; } = new List<GitHubRelease>();
}
