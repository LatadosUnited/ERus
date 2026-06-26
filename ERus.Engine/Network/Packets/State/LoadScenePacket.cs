using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.State;

public class LoadScenePacket : INetSerializable
{
    public string SceneName { get; set; } = "";

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(SceneName);
    }

    public void Deserialize(NetDataReader reader)
    {
        SceneName = reader.GetString();
    }
}
