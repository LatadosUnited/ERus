using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.Events;

public class UserPresencePacket : INetSerializable
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int SelectedNetworkId { get; set; } = -1;
    public byte ColorR { get; set; } = 255;
    public byte ColorG { get; set; } = 255;
    public byte ColorB { get; set; } = 255;
    public bool IsDisconnecting { get; set; } = false;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(UserId);
        writer.Put(Username ?? "");
        writer.Put(SelectedNetworkId);
        writer.Put(ColorR);
        writer.Put(ColorG);
        writer.Put(ColorB);
        writer.Put(IsDisconnecting);
    }

    public void Deserialize(NetDataReader reader)
    {
        UserId = reader.GetInt();
        Username = reader.GetString();
        SelectedNetworkId = reader.GetInt();
        ColorR = reader.GetByte();
        ColorG = reader.GetByte();
        ColorB = reader.GetByte();
        IsDisconnecting = reader.GetBool();
    }
}
