---
name: erus-network-packet
description: Use this skill when you need to create a new network packet, synchronize state, or handle LiteNetLib network messages.
---

# ERus Network Packet Pipeline

The ERus engine uses `LiteNetLib` for networking. When you need to synchronize state across the network, you must create a specific Packet struct/class and register it.

## 1. Defining the Packet
Packets must implement `INetSerializable` from `LiteNetLib.Utils`.
- Location: `ERus.Engine/Network/Packets/`

```csharp
using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets;

public struct MyCustomSyncPacket : INetSerializable
{
    public int NetworkId;
    public float MyValue;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(MyValue);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        MyValue = reader.GetFloat();
    }
}
```

## 2. Registering the Packet
All packets must be registered in the `NetworkManager` so the `NetworkPacketDispatcher` knows how to route them.
- Location: `ERus.Engine/Network/NetworkManager.cs` (or inside specific Synchronizers like `WorldStateSynchronizer.cs`).

```csharp
// Inside WorldStateSynchronizer.cs or similar class setup:
dispatcher.SubscribeReusable<MyCustomSyncPacket>((packet, peer) => 
{
    OnMyCustomPacketReceived(packet, peer);
});
```

## 3. Sending the Packet
To send the packet, you will generally use methods inside `WorldStateSynchronizer.cs` or `ReplicationSystem`, calling `NetworkManager.Transport`.

```csharp
public void SendCustomSync(int networkId, float value)
{
    var packet = new MyCustomSyncPacket { NetworkId = networkId, MyValue = value };
    
    // Send to server if we are client
    if (!NetworkManager.IsHost) {
        NetworkManager.Transport.SendToServer(packet, DeliveryMethod.ReliableOrdered);
    } 
    // Broadcast to all clients if we are host
    else {
        NetworkManager.Transport.Broadcast(packet, DeliveryMethod.ReliableOrdered);
    }
}
```

## Tips
- Always check if an entity has a `NetworkIdentityComponent` before attempting to sync it!
- The Host is the authority. Clients usually send commands to the Host, and the Host broadcasts the state to everyone.
