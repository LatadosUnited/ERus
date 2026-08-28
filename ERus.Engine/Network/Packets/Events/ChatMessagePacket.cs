using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.Events;

public class ChatMessagePacket : INetSerializable
{
    public int SenderId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(SenderId);
        writer.Put(Username ?? "");
        writer.Put(Message ?? "");
        writer.Put(Timestamp ?? "");
    }

    public void Deserialize(NetDataReader reader)
    {
        SenderId = reader.GetInt();
        Username = reader.GetString();
        Message = reader.GetString();
        Timestamp = reader.GetString();
    }
}
