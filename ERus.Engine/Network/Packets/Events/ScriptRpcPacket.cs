using LiteNetLib.Utils;
using System;

namespace ERus.Engine.Network.Packets.Events;

/// <summary>
/// Pacote que trafega chamadas RPC de scripts entre Cliente e Servidor.
/// </summary>
public class ScriptRpcPacket : INetSerializable
{
    public int NetworkId { get; set; }
    public string ScriptTypeName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public bool IsServerRpc { get; set; }
    public string[] Arguments { get; set; } = Array.Empty<string>();

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(ScriptTypeName ?? string.Empty);
        writer.Put(MethodName ?? string.Empty);
        writer.Put(IsServerRpc);
        writer.Put(Arguments.Length);
        for (int i = 0; i < Arguments.Length; i++)
        {
            writer.Put(Arguments[i] ?? string.Empty);
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        ScriptTypeName = reader.GetString();
        MethodName = reader.GetString();
        IsServerRpc = reader.GetBool();
        int count = reader.GetInt();
        Arguments = new string[count];
        for (int i = 0; i < count; i++)
        {
            Arguments[i] = reader.GetString();
        }
    }
}
