using ERus.Engine.Network.Packets.Events;
using ERus.Engine.Network.Packets.State;
using LiteNetLib.Utils;
using Xunit;
using System.Collections.Generic;

namespace ERus.Tests;

public class PacketSerializationTests
{
    [Fact]
    public void UpdateCameraPacket_Serialization_Works()
    {
        var original = new UpdateCameraPacket
        {
            NetworkId = 42,
            FieldOfView = 60.5f,
            IsPrimary = true,
            NearClip = 0.1f,
            FarClip = 100f
        };

        var writer = new NetDataWriter();
        original.Serialize(writer);

        var reader = new NetDataReader(writer.Data);
        var deserialized = new UpdateCameraPacket();
        deserialized.Deserialize(reader);

        Assert.Equal(original.NetworkId, deserialized.NetworkId);
        Assert.Equal(original.FieldOfView, deserialized.FieldOfView);
        Assert.Equal(original.IsPrimary, deserialized.IsPrimary);
        Assert.Equal(original.NearClip, deserialized.NearClip);
        Assert.Equal(original.FarClip, deserialized.FarClip);
    }

    [Fact]
    public void UpdatePhysicsPacket_Serialization_Works()
    {
        var original = new UpdatePhysicsPacket
        {
            NetworkId = 99,
            Mass = 5.5f,
            LinearDrag = 0.2f,
            AngularDrag = 0.1f,
            UseGravity = true,
            IsKinematic = false,
            Constraints = 3 // (int)RigidbodyConstraints.FreezePositionX | FreezePositionY
        };

        var writer = new NetDataWriter();
        original.Serialize(writer);

        var reader = new NetDataReader(writer.Data);
        var deserialized = new UpdatePhysicsPacket();
        deserialized.Deserialize(reader);

        Assert.Equal(original.NetworkId, deserialized.NetworkId);
        Assert.Equal(original.Mass, deserialized.Mass);
        Assert.Equal(original.LinearDrag, deserialized.LinearDrag);
        Assert.Equal(original.AngularDrag, deserialized.AngularDrag);
        Assert.Equal(original.UseGravity, deserialized.UseGravity);
        Assert.Equal(original.IsKinematic, deserialized.IsKinematic);
        Assert.Equal(original.Constraints, deserialized.Constraints);
    }

    [Fact]
    public void UpdateScriptPacket_Serialization_Works()
    {
        var original = new UpdateScriptPacket
        {
            NetworkId = 7,
            Scripts = new[]
            {
                new ScriptPacketData
                {
                    ScriptTypeName = "PlayerController",
                    FieldValues = new Dictionary<string, string>
                    {
                        { "Speed", "10" },
                        { "JumpForce", "5" }
                    }
                }
            }
        };

        var processor = new NetPacketProcessor();
        processor.RegisterNestedType<ScriptPacketData>(
            (w, data) => {
                w.Put(data.ScriptTypeName);
                w.Put(data.FieldValues.Count);
                foreach (var kvp in data.FieldValues) {
                    w.Put(kvp.Key);
                    w.Put(kvp.Value);
                }
            },
            r => {
                var data = new ScriptPacketData();
                data.ScriptTypeName = r.GetString();
                int fieldCount = r.GetInt();
                data.FieldValues = new Dictionary<string, string>(fieldCount);
                for (int f = 0; f < fieldCount; f++) {
                    data.FieldValues[r.GetString()] = r.GetString();
                }
                return data;
            }
        );

        var writer = new NetDataWriter();
        processor.Write(writer, original);

        var reader = new NetDataReader(writer.Data);
        UpdateScriptPacket? deserialized = null;
        processor.SubscribeReusable<UpdateScriptPacket, LiteNetLib.NetPeer>((packet, peer) => { deserialized = packet; });
        processor.ReadAllPackets(reader, null);

        Assert.NotNull(deserialized);
        Assert.Equal(original.NetworkId, deserialized.NetworkId);
        Assert.Single(deserialized.Scripts);
        Assert.Equal(original.Scripts[0].ScriptTypeName, deserialized.Scripts[0].ScriptTypeName);
        Assert.Equal(original.Scripts[0].FieldValues.Count, deserialized.Scripts[0].FieldValues.Count);
        Assert.Equal(original.Scripts[0].FieldValues["Speed"], deserialized.Scripts[0].FieldValues["Speed"]);
        Assert.Equal(original.Scripts[0].FieldValues["JumpForce"], deserialized.Scripts[0].FieldValues["JumpForce"]);
    }

    [Fact]
    public void LoadScenePacket_Serialization_Works()
    {
        var original = new LoadScenePacket
        {
            SceneName = "Assets/Scenes/Level1.scene"
        };

        var writer = new NetDataWriter();
        original.Serialize(writer);

        var reader = new NetDataReader(writer.Data);
        var deserialized = new LoadScenePacket();
        deserialized.Deserialize(reader);

        Assert.Equal(original.SceneName, deserialized.SceneName);
    }
}
