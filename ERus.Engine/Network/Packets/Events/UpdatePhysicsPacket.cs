using LiteNetLib.Utils;
using ERus.Engine.ECS;

namespace ERus.Engine.Network.Packets.Events;

public class UpdatePhysicsPacket : INetSerializable
{
    public int NetworkId { get; set; }
    public float Mass { get; set; }
    public float LinearDrag { get; set; }
    public float AngularDrag { get; set; }
    public bool UseGravity { get; set; }
    public bool IsKinematic { get; set; }
    public int Constraints { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(Mass);
        writer.Put(LinearDrag);
        writer.Put(AngularDrag);
        writer.Put(UseGravity);
        writer.Put(IsKinematic);
        writer.Put(Constraints);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        Mass = reader.GetFloat();
        LinearDrag = reader.GetFloat();
        AngularDrag = reader.GetFloat();
        UseGravity = reader.GetBool();
        IsKinematic = reader.GetBool();
        Constraints = reader.GetInt();
    }
}
