using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.Assets;

public class AssetAnnouncePacket : INetSerializable
{
    public string Hash { get; set; }
    public string FileName { get; set; }
    public long FileSize { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Hash ?? string.Empty);
        writer.Put(FileName ?? string.Empty);
        writer.Put(FileSize);
    }

    public void Deserialize(NetDataReader reader)
    {
        Hash = reader.GetString();
        FileName = reader.GetString();
        FileSize = reader.GetLong();
    }
}
