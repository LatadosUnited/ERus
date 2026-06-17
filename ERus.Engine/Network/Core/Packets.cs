using LiteNetLib.Utils;
using Silk.NET.Maths;

namespace ERus.Engine.Network.Core;

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
            writer.Put(Rotation.X); writer.Put(Rotation.Y); writer.Put(Rotation.Z);
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
            Rotation = new Vector3D<float>(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }
        if ((UpdateFlags & 4) != 0)
        {
            Scale = new Vector3D<float>(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }
    }
}

public class SpawnEntityPacket : INetSerializable
{
    public int NetworkId { get; set; }
    public string Tag { get; set; }
    public int MeshType { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(Tag ?? string.Empty);
        writer.Put(MeshType);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        Tag = reader.GetString();
        MeshType = reader.GetInt();
    }
}

public class LockPacket : INetSerializable
{
    public int NetworkId { get; set; }
    public int UserId { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(UserId);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        UserId = reader.GetInt();
    }
}

public class UnlockPacket : INetSerializable
{
    public int NetworkId { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
    }
}

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

public class DestroyEntityPacket : INetSerializable
{
    public int NetworkId { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
    }
}

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
