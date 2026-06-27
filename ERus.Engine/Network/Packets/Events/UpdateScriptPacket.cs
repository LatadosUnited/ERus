using LiteNetLib.Utils;
using System.Collections.Generic;

namespace ERus.Engine.Network.Packets.Events;

public class UpdateScriptPacket
{
    public int NetworkId { get; set; }
    
    // Lista de Scripts (Name e pares Key/Value)
    public ScriptPacketData[] Scripts { get; set; } = System.Array.Empty<ScriptPacketData>();


}

public class ScriptPacketData
{
    public string ScriptTypeName { get; set; } = "";
    public Dictionary<string, string> FieldValues { get; set; } = new Dictionary<string, string>();
}
