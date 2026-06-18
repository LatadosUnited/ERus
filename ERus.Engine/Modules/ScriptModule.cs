using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    private readonly List<WeakReference> _unloadingContexts = new();

    public string ScriptsPath { get; private set; } = "";

    /// <summary>
    /// Evento disparado quando os scripts são recompilados com sucesso (útil para sistemas que instanciam scripts).
    /// </summary>
    public event Action? OnRecompiled;

    /// <summary>
    /// Evento disparado ANTES da recompilação e do unload. Útil para limpar instâncias que seguram o assembly antigo.
    /// </summary>
    public event Action? OnBeforeRecompile;

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

        // Monitorar ALCs antigos
        for (int i = _unloadingContexts.Count - 1; i >= 0; i--)
        {
            if (!_unloadingContexts[i].IsAlive)
            {
                _unloadingContexts.RemoveAt(i);
                ConsoleLog.Log("AssemblyLoadContext antigo coletado com sucesso pelo GC.");
            }
        }
    }

    public void Render(double deltaTime)
    {
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        UnloadAndForceGC();
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference UnloadAndForceGC()
    {
        WeakReference weakRef = new WeakReference(null);
        if (_currentLoadContext != null)
        {
            weakRef = new WeakReference(_currentLoadContext);
            _currentLoadContext.Unload();
            _currentLoadContext = null;
        }
        
        // Limpar o cache de reflexão antigo para não segurar o ALC
        _compilationResult = null;

        return weakRef;
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

        // 1. Avisar todos para soltarem referências
        OnBeforeRecompile?.Invoke();

        // 2. Descarregar o contexto de forma isolada e obter referência fraca
        var weakRef = UnloadAndForceGC();

        // 3. Forçar GC para limpar objetos e finalmente o contexto
        for (int i = 0; i < 2; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        if (weakRef.IsAlive)
        {
            _unloadingContexts.Add(weakRef);
            ConsoleLog.Warn("ALC não foi coletado imediatamente. Adicionado à lista de monitoramento.");
        }

        // 4. Compilar novo assembly
        CompileAll();

        if (_compilationResult?.Success == true)
        {
            OnRecompiled?.Invoke();
        }
    }
}
