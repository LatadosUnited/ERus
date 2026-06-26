using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ERus.Hub.Services;

public class RemoteServerClient
{
    private readonly HttpClient _httpClient = new HttpClient();

    public async Task<(bool Success, string Token, string Error)> AuthenticateAsync(string ip, string username, string password, bool isRegister)
    {
        try
        {
            var payload = new { Username = username, Password = password };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            string endpoint = isRegister ? "/api/register" : "/api/login";
            var response = await _httpClient.PostAsync($"http://{ip}:8080{endpoint}", content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
                string token = result.GetProperty("token").GetString() ?? "";
                return (true, token, "");
            }
            else
            {
                try
                {
                    var errResult = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    if (errResult.TryGetProperty("error", out var errProp))
                    {
                        return (false, "", errProp.GetString() ?? "Unknown error.");
                    }
                }
                catch { }
                return (false, "", isRegister ? "Registration failed." : "Login failed or Invalid Credentials.");
            }
        }
        catch (Exception ex)
        {
            return (false, "", $"Network Error: {ex.Message}");
        }
    }

    public async Task<(bool Success, List<RemoteProject> Projects, string Error)> FetchProjectsAsync(SavedServer server)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", server.Token);
            var response = await _httpClient.GetAsync($"http://{server.Ip}:8080/api/projects");

            if (response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(body);
                
                var projects = new List<RemoteProject>();
                foreach (var p in result.GetProperty("projects").EnumerateArray())
                {
                    projects.Add(new RemoteProject
                    {
                        Id = p.GetProperty("id").GetString() ?? "",
                        Name = p.GetProperty("name").GetString() ?? "",
                        EngineVersion = p.GetProperty("engineVersion").GetString() ?? "",
                        LastModified = p.GetProperty("lastModified").GetString() ?? ""
                    });
                }
                return (true, projects, "");
            }
            else
            {
                return (false, new List<RemoteProject>(), "Failed to fetch projects. Token might be invalid.");
            }
        }
        catch (Exception ex)
        {
            return (false, new List<RemoteProject>(), $"Failed to connect to server: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Error)> CreateProjectAsync(SavedServer server, string name, string engineVersion)
    {
        try
        {
            var payload = new { Name = name, EngineVersion = engineVersion };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", server.Token);
            var response = await _httpClient.PostAsync($"http://{server.Ip}:8080/api/projects", content);

            if (response.IsSuccessStatusCode)
            {
                return (true, "");
            }
            else
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                try
                {
                    var errResult = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    if (errResult.TryGetProperty("error", out var errProp))
                    {
                        return (false, errProp.GetString() ?? "Unknown error.");
                    }
                }
                catch { }
                return (false, "Failed to create project.");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Network Error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Error)> ChangeProjectVersionAsync(SavedServer server, string projectId, string engineVersion)
    {
        try
        {
            var payload = new { Id = projectId, EngineVersion = engineVersion };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", server.Token);
            var response = await _httpClient.PutAsync($"http://{server.Ip}:8080/api/projects", content);

            if (response.IsSuccessStatusCode)
            {
                return (true, "");
            }
            else
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                try
                {
                    var errResult = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    if (errResult.TryGetProperty("error", out var errProp))
                    {
                        return (false, errProp.GetString() ?? "Unknown error.");
                    }
                }
                catch { }
                return (false, "Failed to update project.");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Network Error: {ex.Message}");
        }
    }
}
