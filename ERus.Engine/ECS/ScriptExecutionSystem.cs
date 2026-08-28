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
            _scriptModule.OnBeforeRecompile += OnBeforeRecompile;
            _scriptModule.OnRecompiled += OnRecompiled;
        }
    }

    public void Dispose()
    {
        if (_scriptModule != null)
        {
            _scriptModule.OnBeforeRecompile -= OnBeforeRecompile;
            _scriptModule.OnRecompiled -= OnRecompiled;
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

    private void OnBeforeRecompile()
    {
        bool wasPlaying = _engine.State == EngineState.Play;
        if (wasPlaying)
        {
            DestroyAllScripts();
        }
    }

    private void OnRecompiled()
    {
        bool wasPlaying = _engine.State == EngineState.Play;
        if (wasPlaying)
        {
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

            bool entityExists = Registry.IsAlive(entity) && Registry.HasComponent<ScriptComponent>(entity);

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

    /// <summary>
    /// Retorna a instância ativa de um script para a entidade especificada.
    /// </summary>
    public ERusScript? GetScriptInstance(Entity entity, string scriptTypeName)
    {
        if (_entityScriptsMap.TryGetValue(entity.Id, out var scripts))
        {
            return scripts.FirstOrDefault(s => s.GetType().Name == scriptTypeName || s.GetType().FullName == scriptTypeName);
        }
        return null;
    }

    /// <summary>
    /// Executa uma chamada RPC em uma entidade ativa.
    /// </summary>
    public void ExecuteRpcOnEntity(Entity entity, string scriptTypeName, string methodName, string[] args)
    {
        var script = GetScriptInstance(entity, scriptTypeName);
        if (script != null)
        {
            script.ExecuteRpcLocal(methodName, args);
        }
        else
        {
            ConsoleLog.Warn($"[Rede] Script '{scriptTypeName}' não encontrado na entidade #{entity.Id} para executar RPC '{methodName}'.");
        }
    }

    /// <summary>
    /// Aplica a atualização de um [SyncVar] na instância do script da entidade.
    /// </summary>
    public void ApplySyncVarOnEntity(Entity entity, string scriptTypeName, string fieldName, string value)
    {
        var script = GetScriptInstance(entity, scriptTypeName);
        if (script == null) return;

        var type = script.GetType();
        var field = type.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var prop = type.GetProperty(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Type? targetType = field?.FieldType ?? prop?.PropertyType;
        if (targetType == null) return;

        object? converted = null;
        try
        {
            if (targetType == typeof(string)) converted = value;
            else if (targetType == typeof(int) && int.TryParse(value, out int i)) converted = i;
            else if (targetType == typeof(float) && float.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float f)) converted = f;
            else if (targetType == typeof(bool) && bool.TryParse(value, out bool b)) converted = b;
            else if (targetType == typeof(double) && double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d)) converted = d;

            if (converted != null)
            {
                if (field != null) field.SetValue(script, converted);
                else prop?.SetValue(script, converted);

                var syncAttr = (field?.GetCustomAttributes(typeof(ERus.Engine.Network.Attributes.SyncVarAttribute), true).FirstOrDefault()
                             ?? prop?.GetCustomAttributes(typeof(ERus.Engine.Network.Attributes.SyncVarAttribute), true).FirstOrDefault()) as ERus.Engine.Network.Attributes.SyncVarAttribute;

                if (!string.IsNullOrEmpty(syncAttr?.Hook))
                {
                    var hookMethod = type.GetMethod(syncAttr.Hook, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (hookMethod != null)
                    {
                        var hookParams = hookMethod.GetParameters();
                        if (hookParams.Length == 1)
                        {
                            hookMethod.Invoke(script, new object?[] { converted });
                        }
                        else if (hookParams.Length == 0)
                        {
                            hookMethod.Invoke(script, null);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[Rede] Erro ao aplicar SyncVar '{fieldName}' no script '{scriptTypeName}': {ex.Message}");
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
