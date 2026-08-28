using System.Numerics;
using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.Events;

public class UpdateMaterialPacket : INetSerializable
{
    public int NetworkId { get; set; }
    public Vector4 ColorTint { get; set; } = Vector4.One;
    public string? TextureHash { get; set; }
    public Vector2 Tiling { get; set; } = Vector2.One;
    public Vector2 Offset { get; set; } = Vector2.Zero;
    public float Metallic { get; set; }
    public float Roughness { get; set; } = 0.5f;
    public bool IsTransparent { get; set; }
    public float AlphaCutoff { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(ColorTint.X);
        writer.Put(ColorTint.Y);
        writer.Put(ColorTint.Z);
        writer.Put(ColorTint.W);
        writer.Put(TextureHash ?? string.Empty);
        writer.Put(Tiling.X);
        writer.Put(Tiling.Y);
        writer.Put(Offset.X);
        writer.Put(Offset.Y);
        writer.Put(Metallic);
        writer.Put(Roughness);
        writer.Put(IsTransparent);
        writer.Put(AlphaCutoff);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        ColorTint = new Vector4(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        TextureHash = reader.GetString();
        if (TextureHash == string.Empty) TextureHash = null;
        Tiling = new Vector2(reader.GetFloat(), reader.GetFloat());
        Offset = new Vector2(reader.GetFloat(), reader.GetFloat());
        Metallic = reader.GetFloat();
        Roughness = reader.GetFloat();
        IsTransparent = reader.GetBool();
        AlphaCutoff = reader.GetFloat();
    }
}
