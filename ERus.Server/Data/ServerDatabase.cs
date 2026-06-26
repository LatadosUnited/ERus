using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ERus.Server.Data;

public class UserAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public List<RemoteProjectData> Projects { get; set; } = new List<RemoteProjectData>();
}

public class RemoteProjectData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = string.Empty;
    public string LastModified { get; set; } = string.Empty;
    
    // Foreign Key
    public int OwnerId { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public UserAccount Owner { get; set; } = null!;
}

public static class ServerDatabase
{
    private static readonly object _lock = new object();

    public static void Initialize()
    {
        lock (_lock)
        {
            using var db = new AppDbContext();
            db.Database.EnsureCreated();
            Console.WriteLine($"[Database] Banco de dados SQLite criptografado inicializado/verificado.");
            Console.WriteLine($"[Database] Contas cadastradas: {db.Accounts.Count()}");
        }
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public static bool RegisterUser(string username, string password)
    {
        lock (_lock)
        {
            using var db = new AppDbContext();
            
            if (db.Accounts.Any(a => a.Username.ToLower() == username.ToLower()))
                return false;

            var account = new UserAccount
            {
                Username = username,
                PasswordHash = HashPassword(password),
                Token = Guid.NewGuid().ToString("N")
            };
            
            account.Projects.Add(new RemoteProjectData
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = "My First Project",
                EngineVersion = "v0.3.0",
                LastModified = DateTime.UtcNow.ToString("O")
            });

            db.Accounts.Add(account);
            db.SaveChanges();
            return true;
        }
    }

    public static string? AuthenticateUser(string username, string password)
    {
        lock (_lock)
        {
            var hash = HashPassword(password);
            using var db = new AppDbContext();
            
            var account = db.Accounts.FirstOrDefault(a => 
                a.Username.ToLower() == username.ToLower() && 
                a.PasswordHash == hash);

            if (account != null)
            {
                account.Token = Guid.NewGuid().ToString("N");
                db.SaveChanges();
                return account.Token;
            }
            return null;
        }
    }

    public static UserAccount? GetAccountByToken(string token)
    {
        lock (_lock)
        {
            using var db = new AppDbContext();
            return db.Accounts.Include(a => a.Projects).FirstOrDefault(a => a.Token == token);
        }
    }

    public static RemoteProjectData? CreateProject(string token, string projectName, string engineVersion)
    {
        lock (_lock)
        {
            using var db = new AppDbContext();
            var account = db.Accounts.Include(a => a.Projects).FirstOrDefault(a => a.Token == token);
            if (account == null) return null;

            var newProject = new RemoteProjectData
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = projectName,
                EngineVersion = engineVersion,
                LastModified = DateTime.UtcNow.ToString("O"),
                OwnerId = account.Id
            };

            account.Projects.Add(newProject);
            db.SaveChanges();
            
            return newProject;
        }
    }

    public static RemoteProjectData? UpdateProjectEngineVersion(string token, string projectId, string newVersion)
    {
        lock (_lock)
        {
            using var db = new AppDbContext();
            var account = db.Accounts.Include(a => a.Projects).FirstOrDefault(a => a.Token == token);
            if (account == null) return null;

            var project = account.Projects.FirstOrDefault(p => p.Id == projectId);
            if (project == null) return null;

            project.EngineVersion = newVersion;
            project.LastModified = DateTime.UtcNow.ToString("O");
            db.SaveChanges();
            
            return project;
        }
    }

    public static bool ValidateProjectAccess(string token, string projectId)
    {
        lock (_lock)
        {
            using var db = new AppDbContext();
            var account = db.Accounts.Include(a => a.Projects).FirstOrDefault(a => a.Token == token);
            if (account == null) return false;

            return account.Projects.Any(p => p.Id == projectId);
        }
    }
}
