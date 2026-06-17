using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using ERus.Engine.Core;
using ERus.Engine.Scripting;

namespace ERus.Engine.Modules;

/// <summary>
/// Módulo responsável pela compilação e hot-reload dos scripts de gameplay do usuário.
/// </summary>
public class ScriptModule : IEngineModule
{
    private Core.Engine _engine;

    // --- Compilação ---
    private CompilationResult? _compilationResult;
    private AssemblyLoadContext? _currentLoadContext;
    
    public IReadOnlyList<Type> AvailableScriptTypes => _compilationResult?.ScriptTypes ?? (IReadOnlyList<Type>)Array.Empty<Type>();
    public IReadOnlyList<CompilationError> LastErrors => _compilationResult?.Errors ?? (IReadOnlyList<CompilationError>)Array.Empty<CompilationError>();

    // --- Hot-reload ---
    private FileSystemWatcher? _watcher;
    private bool _needsRecompile = false;
    private readonly object _recompileLock = new();

    public string ScriptsPath { get; private set; } = "";

    /// <summary>
    /// Evento disparado quando os scripts são recompilados com sucesso (útil para sistemas que instanciam scripts).
    /// </summary>
    public event Action? OnRecompiled;

    public void Initialize(Core.Engine engine)
    {
        _engine = engine;

        ScriptsPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Assets", "Scripts"));
        if (!Directory.Exists(ScriptsPath))
        {
            Directory.CreateDirectory(ScriptsPath);
            ConsoleLog.Log($"Pasta de scripts criada: {ScriptsPath}");
        }

        CompileAll();
        SetupWatcher();

        ConsoleLog.Log("ScriptModule inicializado.");
    }

    public void Update(double deltaTime)
    {
        lock (_recompileLock)
        {
            if (_needsRecompile)
            {
                _needsRecompile = false;
                HandleRecompile();
            }
        }
    }

    public void Render(double deltaTime)
    {
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        UnloadCurrentAssembly();
        ConsoleLog.Log("ScriptModule desligado.");
    }

    private void CompileAll()
    {
        var files = GetScriptFiles();
        if (files.Length == 0)
        {
            ConsoleLog.Log("Nenhum script encontrado em Assets/Scripts/");
            _compilationResult = new CompilationResult(null, null, new List<CompilationError>(), new List<Type>());
            return;
        }

        ConsoleLog.Log($"Compilando {files.Length} script(s)...");
        _compilationResult = ScriptCompiler.Compile(files);
        _currentLoadContext = _compilationResult.LoadContext;

        if (!_compilationResult.Success && _compilationResult.Errors.Count > 0)
        {
            foreach (var error in _compilationResult.Errors)
            {
                if (error.IsWarning)
                    ConsoleLog.Warn($"{error.File}({error.Line},{error.Column}): {error.Message}");
                else
                    ConsoleLog.Error($"{error.File}({error.Line},{error.Column}): {error.Message}");
            }
        }
    }

    private string[] GetScriptFiles()
    {
        if (!Directory.Exists(ScriptsPath))
            return Array.Empty<string>();

        return Directory.GetFiles(ScriptsPath, "*.cs", SearchOption.AllDirectories);
    }

    private void UnloadCurrentAssembly()
    {
        if (_currentLoadContext != null)
        {
            _currentLoadContext.Unload();
            _currentLoadContext = null;
        }
    }

    private void SetupWatcher()
    {
        try
        {
            _watcher = new FileSystemWatcher(ScriptsPath, "*.cs")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnScriptFileChanged;
            _watcher.Created += OnScriptFileChanged;
            _watcher.Deleted += OnScriptFileChanged;
            _watcher.Renamed += (s, e) => OnScriptFileChanged(s, e);
        }
        catch (Exception ex)
        {
            ConsoleLog.Warn($"FileSystemWatcher não pôde ser iniciado: {ex.Message}");
        }
    }

    private void OnScriptFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (_recompileLock)
        {
            _needsRecompile = true;
        }
    }

    private void HandleRecompile()
    {
        ConsoleLog.Log("Mudança detectada nos scripts. Recompilando...");

        UnloadCurrentAssembly();
        CompileAll();

        if (_compilationResult?.Success == true)
        {
            OnRecompiled?.Invoke();
        }
    }
}
