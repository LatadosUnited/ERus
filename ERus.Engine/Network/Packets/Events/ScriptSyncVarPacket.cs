using LiteNetLib.Utils;

namespace ERus.Engine.Network.Packets.Events;

/// <summary>
/// Pacote para sincronizar o valor de um campo [SyncVar] de um script.
/// </summary>
public class ScriptSyncVarPacket : INetSerializable
{
    public int NetworkId { get; set; }
    public string ScriptTypeName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(ScriptTypeName ?? string.Empty);
        writer.Put(FieldName ?? string.Empty);
        writer.Put(Value ?? string.Empty);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        ScriptTypeName = reader.GetString();
        FieldName = reader.GetString();
        Value = reader.GetString();
    }
}
