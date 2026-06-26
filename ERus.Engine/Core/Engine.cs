using System;
using System.Collections.Generic;
using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace ERus.Engine.Core;

public enum EngineState
{
    Edit,
    Play,
    Pause
}

/// <summary>
/// Orquestrador central do ERus Engine.
/// O Engine é agnóstico (cego) às implementações: ele apenas mantém uma lista de <see cref="IEngineModule"/> 
/// e propaga as chamadas do ciclo de vida em ordem de registro.
/// </summary>
public class Engine : IDisposable
{
    private readonly List<IEngineModule> _modules = new List<IEngineModule>();
    public static Engine Instance { get; private set; }
    

    /// <summary>
    /// Barramento de eventos global.
    /// </summary>
    public EventBus EventBus { get; private set; }

    public Engine()
    {
        Instance = this;
        EventBus = new EventBus();
        string assetsDir = System.IO.Path.Combine(System.Environment.CurrentDirectory, "Assets");
        AssetDatabase = new ERus.Engine.Assets.AssetDatabase(assetsDir);
        AssetDatabase.Scan();
    }
    
    /// <summary>
    /// Estado atual da Engine (Edição ou Jogo).
    /// </summary>
    public EngineState State { get; set; } = EngineState.Edit;

    /// <summary>
    /// A janela nativa do SO (Silk.NET).
    /// </summary>
    public IWindow Window { get; private set; }

    /// <summary>
    /// Tamanho atual do framebuffer (pixels físicos), usado para GL.Viewport.
    /// </summary>
    public Vector2D<int> CurrentSize { get; private set; } = new Vector2D<int>(1280, 720);

    /// <summary>
    /// Tamanho lógico da janela (coordenadas do SO), usado para layout do ImGui.
    /// </summary>
    public Vector2D<int> WindowSize { get; private set; } = new Vector2D<int>(1280, 720);

    /// <summary>
    /// Acesso principal ao Wrapper de OpenGL.
    /// </summary>
    public GL Gl { get; private set; }

    /// <summary>
    /// Contexto primário de input.
    /// </summary>
    public IInputContext Input { get; private set; }

    /// <summary>
    /// Banco de dados global de Assets (.meta, GUIDs, Hashes)
    /// </summary>
    public ERus.Engine.Assets.AssetDatabase AssetDatabase { get; private set; }

    /// <summary>
    /// Adiciona um novo subsistema à esteira de execução da Engine.
    /// Os módulos serão atualizados e renderizados na mesma ordem em que foram adicionados.
    /// </summary>
    /// <param name="module">Instância do módulo a ser anexado.</param>
    public void AddModule(IEngineModule module)
    {
        _modules.Add(module);
    }

    /// <summary>
    /// Permite que qualquer parte do sistema recupere a instância de um módulo específico (ex: GraphicsModule).
    /// </summary>
    public T GetModule<T>() where T : class, IEngineModule
    {
        foreach (var module in _modules)
        {
            if (module is T match)
                return match;
        }
        return null;
    }

    /// <summary>
    /// Inicializa a janela, os contextos gráficos e dispara a inicialização de todos os módulos vinculados.
    /// Por fim, trava a thread chamando o Game Loop.
    /// </summary>
    public void Run()
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "ERus 3D Engine";
        
        Window = Silk.NET.Windowing.Window.Create(options);

        Window.Load += () =>
        {
            Gl = Window.CreateOpenGL();
            Input = Window.CreateInput();

            foreach (var module in _modules)
            {
                module.Initialize(this);
            }
        };

        Window.Update += (deltaTime) => Update(deltaTime);
        Window.Render += (deltaTime) => Render(deltaTime);
        Window.Closing += () => Dispose();
        Window.FramebufferResize += (size) => 
        {
            CurrentSize = size;
            Gl?.Viewport(size);
        };
        Window.Resize += (size) =>
        {
            // Rastreia o tamanho lógico da janela (sem DPI scaling) para o layout do ImGui
            WindowSize = size;
        };

        Window.Run();
    }

    /// <summary>
    /// Inicializa os módulos e roda o laço principal sem interface gráfica.
    /// Usado pelo Servidor Dedicado.
    /// </summary>
    public void RunHeadless(int targetFps = 60)
    {
        foreach (var module in _modules)
        {
            module.Initialize(this);
        }

        double targetDelta = 1.0 / targetFps;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        double lastTime = 0;

        Console.WriteLine($"[Engine] Rodando em modo Headless ({targetFps} FPS max).");

        while (true)
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;
            double deltaTime = currentTime - lastTime;

            if (deltaTime >= targetDelta)
            {
                lastTime = currentTime;
                Update(deltaTime);
                // Rede, Lógica, etc, mas sem renderização gráfica.
            }
            else
            {
                // Libera a CPU
                System.Threading.Thread.Sleep(1);
            }
        }
    }

    /// <summary>
    /// Propaga o pulso de Update para os módulos.
    /// Chamado automaticamente pelo laço principal da janela nativa.
    /// </summary>
    /// <param name="deltaTime">Delta em segundos.</param>
    public void Update(double deltaTime)
    {
        foreach (var module in _modules)
        {
            module.Update(deltaTime);
        }
    }

    /// <summary>
    /// Propaga o pulso de Render para os módulos.
    /// Chamado automaticamente pelo laço principal da janela nativa.
    /// </summary>
    /// <param name="deltaTime">Delta em segundos.</param>
    public void Render(double deltaTime)
    {
        foreach (var module in _modules)
        {
            module.Render(deltaTime);
        }
    }

    /// <summary>
    /// Encerra a engine de forma segura, descartando a memória de todos os módulos.
    /// </summary>
    public void Dispose()
    {
        // Ao realizar o Dispose, o ideal é fazer na ordem reversa de criação
        for (int i = _modules.Count - 1; i >= 0; i--)
        {
            _modules[i].Dispose();
        }
        _modules.Clear();
    }
}
