using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.Events;

public class SpawnEntityPacket : INetSerializable
{
    public int NetworkId { get; set; }
    public string Tag { get; set; }
    public int MeshType { get; set; }
    public string? AssetHash { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(Tag ?? string.Empty);
        writer.Put(MeshType);
        writer.Put(AssetHash ?? string.Empty);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        Tag = reader.GetString();
        MeshType = reader.GetInt();
        AssetHash = reader.GetString();
        if (AssetHash == string.Empty) AssetHash = null;
    }
}
