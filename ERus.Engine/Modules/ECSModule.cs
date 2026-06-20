using System.Collections.Generic;
using System.Linq;
using ERus.Engine.Core;
using ERus.Engine.ECS;

namespace ERus.Engine.Modules;

/// <summary>
/// Módulo orquestrador do Entity Component System.
/// Instancia o Registry global e itera as chamadas de Update em todos os BaseSystem registrados.
/// </summary>
public class ECSModule : IEngineModule
{
    /// <summary>
    /// A cena ativa que envelopa o Registry e todas as entidades.
    /// </summary>
    public Scene ActiveScene { get; private set; }

    private Core.Engine _engine;
    
    private readonly List<BaseSystem> _systems = new();

    public void Initialize(Core.Engine engine)
    {
        _engine = engine;
        ActiveScene = new Scene();

        // Registrar os sistemas base (A ordem importa!)
        var physicsSystem = new PhysicsSystem(ActiveScene.Registry, _engine);
        _systems.Add(physicsSystem);

        var animatorSystem = new AnimatorSystem(ActiveScene.Registry, _engine);
        _systems.Add(animatorSystem);

        var scriptSystem = new ScriptExecutionSystem(ActiveScene.Registry, _engine);
        _systems.Add(scriptSystem);
    }

    public void AddSystem(BaseSystem system)
    {
        _systems.Add(system);
    }

    public T? GetSystem<T>() where T : BaseSystem
    {
        return _systems.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// Chama a lógica dos sistemas do jogo a cada frame.
    /// </summary>
    public void Update(double deltaTime)
    {
        foreach (var system in _systems)
        {
            system.Update(deltaTime);
        }
    }

    public void Render(double deltaTime)
    {
        // ECS raramente renderiza diretamente.
    }

    public void Dispose()
    {
        foreach (var system in _systems)
        {
            if (system is System.IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _systems.Clear();
    }
}
