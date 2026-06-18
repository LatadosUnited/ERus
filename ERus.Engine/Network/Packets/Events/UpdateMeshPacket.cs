using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.Events;

public class UpdateMeshPacket : INetSerializable
{
    public int NetworkId { get; set; }
    public int MeshType { get; set; }
    public string? AssetHash { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(MeshType);
        writer.Put(AssetHash ?? string.Empty);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        MeshType = reader.GetInt();
        AssetHash = reader.GetString();
        if (AssetHash == string.Empty) AssetHash = null;
    }
}
