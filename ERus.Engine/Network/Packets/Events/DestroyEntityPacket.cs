using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.Events;

public class DestroyEntityPacket : INetSerializable
{
    public int NetworkId { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
    }
}
