using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.State;

public class EngineStatePacket : INetSerializable
{
    public byte State { get; set; } // 0: Edit, 1: Play, 2: Pause

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(State);
    }

    public void Deserialize(NetDataReader reader)
    {
        State = reader.GetByte();
    }
}
