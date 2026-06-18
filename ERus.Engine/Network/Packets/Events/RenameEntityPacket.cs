using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.Events;

public class RenameEntityPacket : INetSerializable
{
    public int NetworkId { get; set; }
    public string NewTag { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(NewTag ?? string.Empty);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        NewTag = reader.GetString();
    }
}
