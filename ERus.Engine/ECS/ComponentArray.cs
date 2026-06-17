using System;
using System.Collections.Generic;

namespace ERus.Engine.ECS;

public interface IComponentArray
{
    void EntityDestroyed(Entity entity);
}

public class ComponentArray<T> : IComponentArray where T : struct, IComponent
{
    private readonly T[] _components;
    private readonly Dictionary<int, int> _entityToIndex;
    private readonly Dictionary<int, int> _indexToEntity;
    private int _size;

    public ComponentArray(int maxSize = 5000)
    {
        _components = new T[maxSize];
        _entityToIndex = new Dictionary<int, int>();
        _indexToEntity = new Dictionary<int, int>();
        _size = 0;
    }

    public void InsertData(Entity entity, T component)
    {
        if (_entityToIndex.ContainsKey(entity.Id))
            throw new Exception("Entity already has this component.");

        int newIndex = _size;
        _entityToIndex[entity.Id] = newIndex;
        _indexToEntity[newIndex] = entity.Id;
        _components[newIndex] = component;
        _size++;
    }

    public void RemoveData(Entity entity)
    {
        if (!_entityToIndex.ContainsKey(entity.Id))
            return;

        int indexOfRemovedEntity = _entityToIndex[entity.Id];
        int indexOfLastElement = _size - 1;

        // Move o último elemento para a posição removida (Data Locality)
        _components[indexOfRemovedEntity] = _components[indexOfLastElement];

        int entityOfLastElement = _indexToEntity[indexOfLastElement];
        _entityToIndex[entityOfLastElement] = indexOfRemovedEntity;
        _indexToEntity[indexOfRemovedEntity] = entityOfLastElement;

        _entityToIndex.Remove(entity.Id);
        _indexToEntity.Remove(indexOfLastElement);
        _size--;
    }

    public ref T GetData(Entity entity)
    {
        return ref _components[_entityToIndex[entity.Id]];
    }

    public bool HasData(Entity entity)
    {
        return _entityToIndex.ContainsKey(entity.Id);
    }

    public void EntityDestroyed(Entity entity)
    {
        if (_entityToIndex.ContainsKey(entity.Id))
        {
            RemoveData(entity);
        }
    }
}
