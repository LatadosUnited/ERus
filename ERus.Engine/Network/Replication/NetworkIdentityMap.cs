using System.Collections.Generic;
using ERus.Engine.ECS;

namespace ERus.Engine.Network.Replication;

public class NetworkIdentityMap
{
    private readonly Dictionary<int, Entity> _networkToLocalMap = new Dictionary<int, Entity>();
    private int _nextNetworkId = 1;

    public void ClearLocalMap()
    {
        _networkToLocalMap.Clear();
        _nextNetworkId = 1;
    }

    public int AssignNetworkId(Registry registry, Entity entity)
    {
        int netId = _nextNetworkId++;
        _networkToLocalMap[netId] = entity;
        
        if (!registry.HasComponent<NetworkIdentityComponent>(entity))
            registry.AddComponent(entity, new NetworkIdentityComponent { NetworkId = netId, LockUserId = -1 });
            
        return netId;
    }

    public int GetNetworkId(Registry registry, Entity entity)
    {
        if (registry.HasComponent<NetworkIdentityComponent>(entity))
        {
            return registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
        }
        return -1;
    }

    public bool TryGetEntity(int networkId, out Entity entity)
    {
        return _networkToLocalMap.TryGetValue(networkId, out entity);
    }

    public void Remove(int networkId)
    {
        _networkToLocalMap.Remove(networkId);
    }

    public void Map(int networkId, Entity entity)
    {
        _networkToLocalMap[networkId] = entity;
    }

    public IEnumerable<KeyValuePair<int, Entity>> GetAllMappings()
    {
        return _networkToLocalMap;
    }
}
