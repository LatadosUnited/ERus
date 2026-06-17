using System;
using System.Collections.Generic;
using System.Linq;
using ERus.Engine.Core;
using ERus.Engine.Modules;
using ERus.Engine.Scripting;

namespace ERus.Engine.ECS;

/// <summary>
/// Sistema ECS responsável por executar o ciclo de vida dos scripts de gameplay.
/// Fica responsável por instanciar, chamar Awake, Update e OnDestroy.
/// </summary>
public class ScriptExecutionSystem : BaseSystem, IDisposable
{
    private readonly Core.Engine _engine;
    private readonly ScriptModule _scriptModule;

    // --- Flat list otimizada para o laço principal (Update) ---
    private readonly List<ERusScript> _activeScripts = new();

    // --- Mapa para controle de ciclo de vida (Sincronização e Destruição) ---
    private readonly Dictionary<int, List<ERusScript>> _entityScriptsMap = new();

    // --- Controle do modo Play ---
    private EngineState _previousState = EngineState.Edit;

    public ScriptExecutionSystem(Registry registry, Core.Engine engine) : base(registry)
    {
        _engine = engine;
        _scriptModule = _engine.GetModule<ScriptModule>();
        
        if (_scriptModule != null)
        {
            _scriptModule.OnRecompiled += ReinstantiateAll;
        }
    }

    public void Dispose()
    {
        if (_scriptModule != null)
        {
            _scriptModule.OnRecompiled -= ReinstantiateAll;
        }
        DestroyAllScripts();
    }

    public override void Update(double deltaTime)
    {
        var currentState = _engine.State;

        // Transição Edit/Pause → Play: instanciar scripts
        if (currentState == EngineState.Play && _previousState != EngineState.Play)
        {
            OnEnterPlay();
        }
        // Transição Play → Edit: destruir scripts
        else if (currentState == EngineState.Edit && _previousState != EngineState.Edit)
        {
            OnExitPlay();
        }

        _previousState = currentState;

        // Só executa Update nos scripts durante Play
        if (currentState != EngineState.Play) return;

        // Verificar novas entidades ou alterações de scripts (mantemos isso no loop pois scripts podem ser adicionados em runtime)
        SyncScriptInstances();

        // Chamar Update em todos os scripts vivos de forma linear e contígua (Cache-Friendly!)
        for (int i = 0; i < _activeScripts.Count; i++)
        {
            var script = _activeScripts[i];
            try
            {
                script.DeltaTime = deltaTime;

                if (!script.HasStarted)
                {
                    script.Start();
                    script.HasStarted = true;
                }

                script.Update();
            }
            catch (Exception ex)
            {
                ConsoleLog.Error($"[{script.GetType().Name}] Erro no Update: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Força a reinstanciação dos scripts, usado após hot-reload do ScriptModule.
    /// </summary>
    public void ReinstantiateAll()
    {
        bool wasPlaying = _engine.State == EngineState.Play;
        if (wasPlaying)
        {
            DestroyAllScripts();
            SyncScriptInstances();
        }
    }

    private void OnEnterPlay()
    {
        ConsoleLog.Log("▶ Play Mode — Ativando scripts...");
        SyncScriptInstances();
    }

    private void OnExitPlay()
    {
        ConsoleLog.Log("⏹ Edit Mode — Destruindo scripts...");
        DestroyAllScripts();
    }

    private void SyncScriptInstances()
    {
        var availableTypes = _scriptModule.AvailableScriptTypes;
        if (availableTypes == null || availableTypes.Count == 0) return;

        // 1. Instanciar scripts novos
        foreach (var entity in Registry.View<ScriptComponent>())
        {
            ref var scriptComp = ref Registry.GetComponent<ScriptComponent>(entity);
            
            // Garantir que existe uma lista de controle para esta entidade
            if (!_entityScriptsMap.ContainsKey(entity.Id))
            {
                _entityScriptsMap[entity.Id] = new List<ERusScript>();
            }

            var liveEntityScripts = _entityScriptsMap[entity.Id];

            foreach (var scriptData in scriptComp.Scripts)
            {
                var scriptTypeName = scriptData.ScriptTypeName;
                if (string.IsNullOrEmpty(scriptTypeName)) continue;

                // Verificar se o script já está vivo nesta entidade
                bool alreadyAlive = false;
                foreach (var liveScript in liveEntityScripts)
                {
                    if (liveScript.GetType().Name == scriptTypeName || liveScript.GetType().FullName == scriptTypeName)
                    {
                        alreadyAlive = true;
                        break;
                    }
                }
                if (alreadyAlive) continue;

                // Encontrar o tipo no assembly compilado
                var scriptType = availableTypes.FirstOrDefault(t => t.Name == scriptTypeName || t.FullName == scriptTypeName);

                if (scriptType == null)
                {
                    ConsoleLog.Error($"Script '{scriptTypeName}' não encontrado no assembly compilado.");
                    continue;
                }

                // Instanciar
                try
                {
                    var instance = (ERusScript)Activator.CreateInstance(scriptType)!;
                    instance.Entity = entity;
                    instance.Registry = Registry;
                    instance.Engine = _engine;
                    instance.DeltaTime = 0;
                    instance.HasStarted = false;

                    // Injetar variáveis públicas do Inspector no Script
                    foreach (var kvp in scriptData.FieldValues)
                    {
                        var field = scriptType.GetField(kvp.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            try
                            {
                                object? val = null;
                                if (field.FieldType == typeof(float))
                                {
                                    if (float.TryParse(kvp.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float f)) val = f;
                                }
                                else if (field.FieldType == typeof(int))
                                {
                                    if (int.TryParse(kvp.Value, out int i)) val = i;
                                }
                                else if (field.FieldType == typeof(bool))
                                {
                                    if (bool.TryParse(kvp.Value, out bool b)) val = b;
                                }
                                else if (field.FieldType == typeof(string))
                                {
                                    val = kvp.Value;
                                }
                                
                                if (val != null)
                                {
                                    field.SetValue(instance, val);
                                }
                            }
                            catch (Exception ex)
                            {
                                ConsoleLog.Warn($"Erro ao injetar valor no campo {kvp.Key} do script {scriptType.Name}: {ex.Message}");
                            }
                        }
                    }

                    // Adicionar às listas de controle e execução
                    liveEntityScripts.Add(instance);
                    _activeScripts.Add(instance);

                    instance.Awake();

                    ConsoleLog.Log($"Script '{scriptType.Name}' instanciado na entidade #{entity.Id}");
                }
                catch (Exception ex)
                {
                    ConsoleLog.Error($"Erro ao instanciar '{scriptTypeName}': {ex.Message}");
                }
            }
        }

        // 2. Limpar scripts de entidades mortas ou scripts removidos
        var entitiesToRemove = new List<int>();
        foreach (var kvp in _entityScriptsMap)
        {
            var entityId = kvp.Key;
            var entity = new Entity(entityId);

            bool entityExists = false;
            foreach (var livingEntity in Registry.GetLivingEntities())
            {
                if (livingEntity.Id == entityId && Registry.HasComponent<ScriptComponent>(livingEntity))
                {
                    entityExists = true;
                    break;
                }
            }

            if (!entityExists)
            {
                entitiesToRemove.Add(entityId);
            }
        }

        foreach (var id in entitiesToRemove)
        {
            var scriptsToDestroy = _entityScriptsMap[id];
            foreach (var script in scriptsToDestroy)
            {
                try
                {
                    script.OnDestroy();
                }
                catch (Exception ex)
                {
                    ConsoleLog.Error($"[{script.GetType().Name}] Erro no OnDestroy: {ex.Message}");
                }
                _activeScripts.Remove(script);
            }
            _entityScriptsMap.Remove(id);
        }
    }

    private void DestroyAllScripts()
    {
        for (int i = 0; i < _activeScripts.Count; i++)
        {
            try
            {
                _activeScripts[i].OnDestroy();
            }
            catch (Exception ex)
            {
                ConsoleLog.Error($"[{_activeScripts[i].GetType().Name}] Erro no OnDestroy: {ex.Message}");
            }
        }
        _activeScripts.Clear();
        _entityScriptsMap.Clear();
    }
}
