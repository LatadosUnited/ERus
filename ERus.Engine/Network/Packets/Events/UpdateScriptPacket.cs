using LiteNetLib.Utils;
using System.Collections.Generic;

namespace ERus.Engine.Network.Packets.Events;

public class UpdateScriptPacket : INetSerializable
{
    public int NetworkId { get; set; }
    
    // Lista de Scripts (Name e pares Key/Value)
    public ScriptPacketData[] Scripts { get; set; } = System.Array.Empty<ScriptPacketData>();

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(Scripts.Length);
        for (int i = 0; i < Scripts.Length; i++)
        {
            var s = Scripts[i];
            writer.Put(s.ScriptTypeName ?? string.Empty);
            writer.Put(s.FieldValues?.Count ?? 0);
            if (s.FieldValues != null)
            {
                foreach (var kvp in s.FieldValues)
                {
                    writer.Put(kvp.Key ?? string.Empty);
                    writer.Put(kvp.Value ?? string.Empty);
                }
            }
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        int count = reader.GetInt();
        Scripts = new ScriptPacketData[count];
        for (int i = 0; i < count; i++)
        {
            var item = new ScriptPacketData();
            item.ScriptTypeName = reader.GetString();
            int fieldCount = reader.GetInt();
            item.FieldValues = new Dictionary<string, string>(fieldCount);
            for (int f = 0; f < fieldCount; f++)
            {
                string key = reader.GetString();
                string val = reader.GetString();
                item.FieldValues[key] = val;
            }
            Scripts[i] = item;
        }
    }
}

public class ScriptPacketData
{
    public string ScriptTypeName { get; set; } = "";
    public Dictionary<string, string> FieldValues { get; set; } = new Dictionary<string, string>();
}
