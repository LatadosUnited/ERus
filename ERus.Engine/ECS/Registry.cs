using System;
using System.Collections.Generic;

namespace ERus.Engine.ECS;

public class Registry
{
    private int _nextEntityId = 0;
    private readonly Queue<int> _availableEntities = new Queue<int>();
    private readonly List<Entity> _livingEntities = new List<Entity>();

    private readonly Dictionary<Type, IComponentArray> _componentArrays = new Dictionary<Type, IComponentArray>();

    public IReadOnlyList<Entity> GetLivingEntities()
    {
        return _livingEntities.AsReadOnly();
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
        foreach (var entity in _livingEntities)
        {
            if (array.HasData(entity))
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

        foreach (var entity in _livingEntities)
        {
            if (array1.HasData(entity) && array2.HasData(entity))
            {
                yield return entity;
            }
        }
    }
}
