using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.Auth;

public class AuthRequestPacket : INetSerializable
{
    public string Token { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Token ?? "");
        writer.Put(ProjectId ?? "");
    }

    public void Deserialize(NetDataReader reader)
    {
        Token = reader.GetString();
        ProjectId = reader.GetString();
    }
}

public class AuthResponsePacket : INetSerializable
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Success);
        writer.Put(ErrorMessage ?? "");
    }

    public void Deserialize(NetDataReader reader)
    {
        Success = reader.GetBool();
        ErrorMessage = reader.GetString();
    }
}
