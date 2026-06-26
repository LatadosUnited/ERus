using LiteNetLib.Utils;
using System.Collections.Generic;

namespace ERus.Engine.Network.Packets.Events;

public class UpdateScriptPacket : INetSerializable
{
    public int NetworkId { get; set; }
    
    // Lista de Scripts (Name e pares Key/Value)
    public List<ScriptPacketData> Scripts { get; set; } = new List<ScriptPacketData>();

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetworkId);
        writer.Put(Scripts.Count);
        foreach (var s in Scripts)
        {
            writer.Put(s.ScriptTypeName);
            writer.Put(s.FieldValues.Count);
            foreach (var kvp in s.FieldValues)
            {
                writer.Put(kvp.Key);
                writer.Put(kvp.Value);
            }
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        NetworkId = reader.GetInt();
        int count = reader.GetInt();
        Scripts = new List<ScriptPacketData>(count);
        for (int i = 0; i < count; i++)
        {
            var data = new ScriptPacketData();
            data.ScriptTypeName = reader.GetString();
            int fieldCount = reader.GetInt();
            data.FieldValues = new Dictionary<string, string>(fieldCount);
            for (int f = 0; f < fieldCount; f++)
            {
                string key = reader.GetString();
                string val = reader.GetString();
                data.FieldValues[key] = val;
            }
            Scripts.Add(data);
        }
    }
}

public class ScriptPacketData
{
    public string ScriptTypeName { get; set; } = "";
    public Dictionary<string, string> FieldValues { get; set; } = new Dictionary<string, string>();
}
