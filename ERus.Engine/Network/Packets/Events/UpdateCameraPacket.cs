using LiteNetLib.Utils;
using System.Collections.Generic;

namespace ERus.Engine.Network.Packets.Events;

public class UpdateCameraPacket : INetSerializable
{
    public int NetworkId { get; set; }
    public float FieldOfView { get; set; }
    public bool IsPrimary { get; set; }
    public float NearClip { get; set; }
    public float FarClip { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(FieldOfView);
        writer.Put(IsPrimary);
        writer.Put(NearClip);
        writer.Put(FarClip);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        FieldOfView = reader.GetFloat();
        IsPrimary = reader.GetBool();
        NearClip = reader.GetFloat();
        FarClip = reader.GetFloat();
    }
}
