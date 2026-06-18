using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ERus.Hub;

public class GitHubReleaseManager
{
    private readonly HttpClient _httpClient;
    
    // Você pode alterar isso depois para o repositório correto.
    // Exemplo: "Leandro/ERus"
    private const string RepoOwner = "LatadosUnited";
    private const string RepoName = "ERus";

    public GitHubReleaseManager()
    {
        _httpClient = new HttpClient();
        // GitHub API requires a User-Agent header
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ERus-Hub-Client");
    }

    public async Task<List<GitHubRelease>> GetAvailableReleasesAsync()
    {
        try
        {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases";
            var releases = await _httpClient.GetFromJsonAsync<List<GitHubRelease>>(url);
            return releases ?? new List<GitHubRelease>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GitHubReleaseManager] Failed to fetch releases: {ex.Message}");
            return new List<GitHubRelease>();
        }
    }

    public async Task DownloadAndInstallAsync(GitHubRelease release, GitHubAsset asset, HubConfig config, Action<float> onProgress)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string downloadDir = Path.Combine(appData, "ERusHub", "Downloads");
        
        string baseExtractDir = string.IsNullOrEmpty(config.DefaultInstallDirectory) 
            ? Path.Combine(appData, "ERusHub", "Engines") 
            : config.DefaultInstallDirectory;
            
        string extractDir = Path.Combine(baseExtractDir, release.TagName);

        if (!Directory.Exists(downloadDir)) Directory.CreateDirectory(downloadDir);
        if (!Directory.Exists(extractDir)) Directory.CreateDirectory(extractDir);

        string zipPath = Path.Combine(downloadDir, asset.Name);

        try
        {
            // 1. Download
            using (var response = await _httpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var totalRead = 0L;
                    var buffer = new byte[8192];
                    var isMoreToRead = true;

                    do
                    {
                        var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                        if (read == 0)
                        {
                            isMoreToRead = false;
                        }
                        else
                        {
                            await fileStream.WriteAsync(buffer, 0, read);

                            totalRead += read;
                            if (totalBytes.HasValue)
                            {
                                float progress = (float)totalRead / totalBytes.Value;
                                onProgress?.Invoke(progress * 0.5f); // Primeiros 50% são do download
                            }
                        }
                    } while (isMoreToRead);
                }
            }

            // 2. Extrair
            onProgress?.Invoke(0.5f);
            
            // Limpa pasta se já existir algo para não misturar arquivos de versões corrompidas
            if (Directory.Exists(extractDir))
            {
                Directory.Delete(extractDir, true);
                Directory.CreateDirectory(extractDir);
            }

            ZipFile.ExtractToDirectory(zipPath, extractDir);
            onProgress?.Invoke(0.9f);

            // 3. Registrar no ConfigManager
            // Procurando o executável (ERus.Editor.exe)
            string[] exes = Directory.GetFiles(extractDir, "ERus.Editor.exe", SearchOption.AllDirectories);
            string exePath = exes.Length > 0 ? exes[0] : Path.Combine(extractDir, "ERus.Editor.exe");

            var newInstall = new EngineInstall
            {
                VersionName = release.TagName,
                ExecutablePath = exePath
            };

            // Remove se já existia essa mesma versão instalada (para atualizar)
            config.Installs.RemoveAll(i => i.VersionName == release.TagName);
            config.Installs.Add(newInstall);
            
            ConfigManager.Save(config);

            onProgress?.Invoke(1.0f);
            
            // Cleanup ZIP
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GitHubReleaseManager] Failed to download or install: {ex.Message}");
            throw;
        }
    }
}
