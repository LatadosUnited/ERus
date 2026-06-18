using LiteNetLib.Utils;
using Silk.NET.Maths;

namespace ERus.Engine.Network.Packets.State;

public class TransformPacket : INetSerializable
{
    public int NetworkId { get; set; }
    public uint Tick { get; set; }
    public byte UpdateFlags { get; set; } // 1: Position, 2: Rotation, 4: Scale

    public Vector3D<float> Position { get; set; }
    public Vector3D<float> Rotation { get; set; }
    public Vector3D<float> Scale { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(Tick);
        writer.Put(UpdateFlags);
        
        if ((UpdateFlags & 1) != 0)
        {
            writer.Put(Position.X); writer.Put(Position.Y); writer.Put(Position.Z);
        }
        if ((UpdateFlags & 2) != 0)
        {
            writer.Put(System.BitConverter.HalfToUInt16Bits((System.Half)Rotation.X));
            writer.Put(System.BitConverter.HalfToUInt16Bits((System.Half)Rotation.Y));
            writer.Put(System.BitConverter.HalfToUInt16Bits((System.Half)Rotation.Z));
        }
        if ((UpdateFlags & 4) != 0)
        {
            writer.Put(Scale.X); writer.Put(Scale.Y); writer.Put(Scale.Z);
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        Tick = reader.GetUInt();
        UpdateFlags = reader.GetByte();

        if ((UpdateFlags & 1) != 0)
        {
            Position = new Vector3D<float>(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }
        if ((UpdateFlags & 2) != 0)
        {
            Rotation = new Vector3D<float>(
                (float)System.BitConverter.UInt16BitsToHalf(reader.GetUShort()),
                (float)System.BitConverter.UInt16BitsToHalf(reader.GetUShort()),
                (float)System.BitConverter.UInt16BitsToHalf(reader.GetUShort())
            );
        }
        if ((UpdateFlags & 4) != 0)
        {
            Scale = new Vector3D<float>(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }
    }
}
