using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.Events;

public class LockPacket : INetSerializable
{
    public int NetworkId { get; set; }
    public int UserId { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(UserId);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        UserId = reader.GetInt();
    }
}
