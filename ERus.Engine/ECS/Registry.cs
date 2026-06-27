using System;
using System.Collections.Generic;

namespace ERus.Engine.ECS;

public class Registry
{
    private int _nextEntityId = 0;
    private Queue<int> _availableEntities = new Queue<int>();
    private HashSet<Entity> _livingEntities = new HashSet<Entity>();

    private Dictionary<Type, IComponentArray> _componentArrays = new Dictionary<Type, IComponentArray>();

    public IEnumerable<Entity> GetLivingEntities()
    {
        return _livingEntities;
    }

    public bool IsAlive(Entity entity)
    {
        return _livingEntities.Contains(entity);
    }

    public Entity CreateEntity()
    {
        int id;
        if (_availableEntities.Count > 0)
        {
            id = _availableEntities.Dequeue();
        }
        else
        {
            id = _nextEntityId++;
        }

        var entity = new Entity(id);
        _livingEntities.Add(entity);
        return entity;
    }

    public void DestroyEntity(Entity entity)
    {
        _livingEntities.Remove(entity);
        foreach (var array in _componentArrays.Values)
        {
            array.EntityDestroyed(entity);
        }
        _availableEntities.Enqueue(entity.Id);
    }

    public void Clear()
    {
        var entitiesToDestroy = new List<Entity>(_livingEntities);
        foreach (var entity in entitiesToDestroy)
        {
            DestroyEntity(entity);
        }
    }

    public void RegisterComponent<T>() where T : struct, IComponent
    {
        _componentArrays.Add(typeof(T), new ComponentArray<T>());
    }

    private ComponentArray<T> GetComponentArray<T>() where T : struct, IComponent
    {
        var type = typeof(T);
        if (!_componentArrays.ContainsKey(type))
            throw new Exception($"Component {type.Name} not registered.");

        return (ComponentArray<T>)_componentArrays[type];
    }

    public void AddComponent<T>(Entity entity, T component) where T : struct, IComponent
    {
        GetComponentArray<T>().InsertData(entity, component);
    }

    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
    {
        GetComponentArray<T>().RemoveData(entity);
    }

    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        return ref GetComponentArray<T>().GetData(entity);
    }

    public bool HasComponent<T>(Entity entity) where T : struct, IComponent
    {
        return GetComponentArray<T>().HasData(entity);
    }

    // View simples (uma entidade deve ter todos os componentes requeridos)
    public IEnumerable<Entity> View<T>() where T : struct, IComponent
    {
        var array = GetComponentArray<T>();
        foreach (var entityId in array.ActiveEntities)
        {
            var entity = new Entity(entityId);
            if (_livingEntities.Contains(entity))
            {
                yield return entity;
            }
        }
    }

    public IEnumerable<Entity> View<T1, T2>() 
        where T1 : struct, IComponent 
        where T2 : struct, IComponent
    {
        var array1 = GetComponentArray<T1>();
        var array2 = GetComponentArray<T2>();

        // Optimização: iterar pelo menor array
        var active1 = array1.ActiveEntities;
        
        foreach (var entityId in active1)
        {
            var entity = new Entity(entityId);
            if (_livingEntities.Contains(entity) && array2.HasData(entity))
            {
                yield return entity;
            }
        }
    }

    // --- Métodos baseados em Type (reflexão) para serialização genérica ---

    /// <summary>
    /// Retorna todos os tipos de componentes registrados neste Registry.
    /// </summary>
    public IEnumerable<Type> GetRegisteredComponentTypes()
    {
        return _componentArrays.Keys;
    }

    /// <summary>
    /// Verifica se a entidade possui um componente de um tipo específico (via Type, sem genérico).
    /// </summary>
    public bool HasComponentByType(Entity entity, Type componentType)
    {
        if (!_componentArrays.TryGetValue(componentType, out var array))
            return false;
        return array.HasData(entity);
    }

    /// <summary>
    /// Obtém o componente de uma entidade como object (boxing). Usado para serialização genérica.
    /// </summary>
    public object GetComponentBoxed(Entity entity, Type componentType)
    {
        if (!_componentArrays.TryGetValue(componentType, out var array))
            throw new Exception($"Component {componentType.Name} not registered.");
        return array.GetDataBoxed(entity);
    }

    /// <summary>
    /// Adiciona um componente (já boxed como object) a uma entidade. Usado para deserialização genérica.
    /// </summary>
    public void AddComponentBoxed(Entity entity, Type componentType, object component)
    {
        if (!_componentArrays.TryGetValue(componentType, out var array))
            throw new Exception($"Component {componentType.Name} not registered.");
        array.InsertDataBoxed(entity, component);
    }

    /// <summary>
    /// Remove um componente de uma entidade por Type.
    /// </summary>
    public void RemoveComponentByType(Entity entity, Type componentType)
    {
        if (_componentArrays.TryGetValue(componentType, out var array))
            array.RemoveDataByEntity(entity);
    }

    // --- Clonagem profunda ---

    /// <summary>
    /// Cria uma cópia profunda (snapshot) deste Registry.
    /// Todas as entidades, IDs e componentes são duplicados.
    /// Não copia componentes marcados com [NonSerializedComponent] (ex: IsDirty).
    /// </summary>
    public Registry Clone()
    {
        var clone = new Registry();
        clone._nextEntityId = _nextEntityId;
        clone._availableEntities = new Queue<int>(_availableEntities);
        clone._livingEntities = new HashSet<Entity>(_livingEntities);
        clone._componentArrays = new Dictionary<Type, IComponentArray>(_componentArrays.Count);
        foreach (var kvp in _componentArrays)
        {
            clone._componentArrays[kvp.Key] = kvp.Value.Clone();
        }
        return clone;
    }
}

