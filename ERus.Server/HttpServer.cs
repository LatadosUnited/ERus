using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ERus.Server.Data;

namespace ERus.Server;

public class HttpServer
{
    private HttpListener _listener;
    private bool _isRunning;

    public void Start(int port)
    {
        _listener = new HttpListener();
        // Permite conexões de qualquer lugar
        _listener.Prefixes.Add($"http://*:{port}/");
        try 
        {
            _listener.Start();
        }
        catch (HttpListenerException)
        {
            // Fallback para localhost caso necessite admin rights no windows para escutar global
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
        }

        _isRunning = true;
        
        Console.WriteLine($"[API] Servidor HTTP rodando na porta {port}");
        
        Task.Run(ListenLoopAsync);
    }

    public void Stop()
    {
        _isRunning = false;
        _listener?.Stop();
        _listener?.Close();
    }

    private async Task ListenLoopAsync()
    {
        while (_isRunning)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = ProcessRequestAsync(context);
            }
            catch (Exception) when (!_isRunning)
            {
                // Ignorar erro se estamos parando
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] Erro ao aceitar request: {ex.Message}");
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // Cors simples
        response.AppendHeader("Access-Control-Allow-Origin", "*");
        
        try
        {
            if (request.HttpMethod == "POST" && request.Url?.AbsolutePath == "/api/login")
            {
                await HandleLoginAsync(request, response);
            }
            else if (request.HttpMethod == "POST" && request.Url?.AbsolutePath == "/api/register")
            {
                await HandleRegisterAsync(request, response);
            }
            else if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/api/projects")
            {
                await HandleGetProjectsAsync(request, response);
            }
            else if (request.HttpMethod == "POST" && request.Url?.AbsolutePath == "/api/projects")
            {
                await HandleCreateProjectAsync(request, response);
            }
            else if (request.HttpMethod == "PUT" && request.Url?.AbsolutePath == "/api/projects")
            {
                await HandleUpdateProjectAsync(request, response);
            }
            else
            {
                response.StatusCode = 404;
                await SendJsonResponse(response, new { error = "Not Found" });
            }
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            await SendJsonResponse(response, new { error = "Internal Server Error", message = ex.Message });
        }
        finally
        {
            response.Close();
        }
    }

    private async Task HandleRegisterAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        string body = await reader.ReadToEndAsync();
        var data = JsonSerializer.Deserialize<LoginRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data == null || string.IsNullOrWhiteSpace(data.Username) || string.IsNullOrWhiteSpace(data.Password))
        {
            response.StatusCode = 400;
            await SendJsonResponse(response, new { error = "Username ou Password inválidos." });
            return;
        }

        bool created = ServerDatabase.RegisterUser(data.Username, data.Password);
        if (!created)
        {
            response.StatusCode = 409; // Conflict
            await SendJsonResponse(response, new { error = "Usuário já existe." });
            return;
        }
        
        string? token = ServerDatabase.AuthenticateUser(data.Username, data.Password);
        var account = ServerDatabase.GetAccountByToken(token ?? "");

        Console.WriteLine($"[API] Conta registrada: {data.Username}");

        response.StatusCode = 200;
        await SendJsonResponse(response, new
        {
            success = true,
            token = token,
            username = data.Username,
            projects = account?.Projects ?? new List<RemoteProjectData>()
        });
    }

    private async Task HandleLoginAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        string body = await reader.ReadToEndAsync();
        var loginData = JsonSerializer.Deserialize<LoginRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (loginData == null || string.IsNullOrWhiteSpace(loginData.Username) || string.IsNullOrWhiteSpace(loginData.Password))
        {
            response.StatusCode = 400;
            await SendJsonResponse(response, new { error = "Credenciais inválidas" });
            return;
        }

        string? token = ServerDatabase.AuthenticateUser(loginData.Username, loginData.Password);

        if (token == null)
        {
            response.StatusCode = 401;
            await SendJsonResponse(response, new { error = "Usuário ou senha incorretos." });
            return;
        }

        var account = ServerDatabase.GetAccountByToken(token);

        var responseData = new
        {
            success = true,
            token = token,
            username = loginData.Username,
            projects = account?.Projects ?? new List<RemoteProjectData>()
        };

        Console.WriteLine($"[API] Login aprovado para: {loginData.Username}");
        
        response.StatusCode = 200;
        await SendJsonResponse(response, responseData);
    }

    private async Task HandleGetProjectsAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        string authHeader = request.Headers["Authorization"] ?? "";
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            response.StatusCode = 401;
            await SendJsonResponse(response, new { error = "Unauthorized" });
            return;
        }

        string token = authHeader.Substring("Bearer ".Length);
        var account = ServerDatabase.GetAccountByToken(token);

        if (account == null)
        {
            response.StatusCode = 401;
            await SendJsonResponse(response, new { error = "Invalid Token" });
            return;
        }

        response.StatusCode = 200;
        await SendJsonResponse(response, new { success = true, projects = account.Projects });
    }

    private async Task HandleCreateProjectAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        string authHeader = request.Headers["Authorization"] ?? "";
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            response.StatusCode = 401;
            await SendJsonResponse(response, new { error = "Unauthorized" });
            return;
        }

        string token = authHeader.Substring("Bearer ".Length);
        var account = ServerDatabase.GetAccountByToken(token);

        if (account == null)
        {
            response.StatusCode = 401;
            await SendJsonResponse(response, new { error = "Invalid Token" });
            return;
        }

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        string body = await reader.ReadToEndAsync();
        var data = JsonSerializer.Deserialize<CreateProjectRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data == null || string.IsNullOrWhiteSpace(data.Name) || string.IsNullOrWhiteSpace(data.EngineVersion))
        {
            response.StatusCode = 400;
            await SendJsonResponse(response, new { error = "Nome e Versão da Engine são obrigatórios." });
            return;
        }

        var newProject = ServerDatabase.CreateProject(token, data.Name, data.EngineVersion);
        if (newProject == null)
        {
            response.StatusCode = 500;
            await SendJsonResponse(response, new { error = "Erro interno ao criar projeto." });
            return;
        }

        Console.WriteLine($"[API] Projeto '{data.Name}' criado por {account.Username}");

        response.StatusCode = 200;
        await SendJsonResponse(response, new { success = true, project = newProject });
    }

    private async Task HandleUpdateProjectAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        string authHeader = request.Headers["Authorization"] ?? "";
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            response.StatusCode = 401;
            await SendJsonResponse(response, new { error = "Unauthorized" });
            return;
        }

        string token = authHeader.Substring("Bearer ".Length);
        var account = ServerDatabase.GetAccountByToken(token);

        if (account == null)
        {
            response.StatusCode = 401;
            await SendJsonResponse(response, new { error = "Invalid Token" });
            return;
        }

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        string body = await reader.ReadToEndAsync();
        var data = JsonSerializer.Deserialize<UpdateProjectRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data == null || string.IsNullOrWhiteSpace(data.Id) || string.IsNullOrWhiteSpace(data.EngineVersion))
        {
            response.StatusCode = 400;
            await SendJsonResponse(response, new { error = "ID do Projeto e Nova Versão da Engine são obrigatórios." });
            return;
        }

        var updatedProject = ServerDatabase.UpdateProjectEngineVersion(token, data.Id, data.EngineVersion);
        if (updatedProject == null)
        {
            response.StatusCode = 404;
            await SendJsonResponse(response, new { error = "Projeto não encontrado ou você não tem permissão." });
            return;
        }

        Console.WriteLine($"[API] Versão do projeto '{updatedProject.Name}' atualizada para {data.EngineVersion}");

        response.StatusCode = 200;
        await SendJsonResponse(response, new { success = true, project = updatedProject });
    }

    private async Task SendJsonResponse(HttpListenerResponse response, object data)
    {
        response.ContentType = "application/json";
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        string json = JsonSerializer.Serialize(data, options);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = string.Empty;
}

public class UpdateProjectRequest
{
    public string Id { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = string.Empty;
}
