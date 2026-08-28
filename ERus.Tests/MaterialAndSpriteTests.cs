using System;
using System.Numerics;
using ERus.Engine.ECS;
using ERus.Engine.Graphics;
using ERus.Engine.Network.Packets.Events;
using LiteNetLib.Utils;
using Xunit;

namespace ERus.Tests;

public class MaterialAndSpriteTests
{
    [Fact]
    public void UpdateMaterialPacket_Serialization_Works()
    {
        var original = new UpdateMaterialPacket
        {
            NetworkId = 123,
            ColorTint = new Vector4(1f, 0.5f, 0.25f, 0.8f),
            TextureHash = "a1b2c3d4e5f67890",
            Tiling = new Vector2(2.0f, 3.0f),
            Offset = new Vector2(0.1f, 0.2f),
            Metallic = 0.8f,
            Roughness = 0.2f,
            IsTransparent = true,
            AlphaCutoff = 0.5f
        };

        var writer = new NetDataWriter();
        original.Serialize(writer);

        var reader = new NetDataReader(writer.Data);
        var deserialized = new UpdateMaterialPacket();
        deserialized.Deserialize(reader);

        Assert.Equal(original.NetworkId, deserialized.NetworkId);
        Assert.Equal(original.ColorTint, deserialized.ColorTint);
        Assert.Equal(original.TextureHash, deserialized.TextureHash);
        Assert.Equal(original.Tiling, deserialized.Tiling);
        Assert.Equal(original.Offset, deserialized.Offset);
        Assert.Equal(original.Metallic, deserialized.Metallic);
        Assert.Equal(original.Roughness, deserialized.Roughness);
        Assert.Equal(original.IsTransparent, deserialized.IsTransparent);
        Assert.Equal(original.AlphaCutoff, deserialized.AlphaCutoff);
    }

    [Fact]
    public void NetworkPacketDispatcher_CanRegisterUpdateMaterialPacket_WithoutInvalidTypeException()
    {
        var transport = new ERus.Engine.Network.Core.NetworkTransport();
        var dispatcher = new ERus.Engine.Network.Core.NetworkPacketDispatcher(transport);

        bool received = false;
        // Não deve lançar InvalidTypeException para Vector4, Vector2, etc.
        dispatcher.SubscribeReusable<UpdateMaterialPacket>((packet, peer) =>
        {
            received = true;
        });

        Assert.False(received);
    }

    [Fact]
    public void MaterialComponent_SceneSerializer_GenericSerialization_Works()
    {
        var scene = new Scene();
        var entity = scene.Registry.CreateEntity();
        scene.Registry.AddComponent(entity, new TransformComponent());
        scene.Registry.AddComponent(entity, new TagComponent { Name = "MaterialTestEntity" });

        var guid = Guid.NewGuid();
        scene.Registry.AddComponent(entity, new MaterialComponent
        {
            ColorTint = new Vector4(0.2f, 0.4f, 0.6f, 1.0f),
            AlbedoTextureGuid = guid,
            AlbedoTextureHash = "hash123",
            Tiling = new Vector2(3f, 4f),
            Offset = new Vector2(0.5f, 0.5f),
            Metallic = 0.7f,
            Roughness = 0.3f,
            IsTransparent = false,
            AlphaCutoff = 0.1f
        });

        string json = SceneSerializer.SerializeEntityToJson(entity, scene.Registry);
        Assert.Contains("MaterialComponent", json);

        var cloneScene = new Scene();
        var restoredEntity = cloneScene.Registry.CreateEntity();
        SceneSerializer.DeserializeEntityFromJson(json, restoredEntity, cloneScene.Registry);

        Assert.True(cloneScene.Registry.HasComponent<MaterialComponent>(restoredEntity));
        var restoredMat = cloneScene.Registry.GetComponent<MaterialComponent>(restoredEntity);
        Assert.Equal(new Vector4(0.2f, 0.4f, 0.6f, 1.0f), restoredMat.ColorTint);
        Assert.Equal(guid, restoredMat.AlbedoTextureGuid);
        Assert.Equal("hash123", restoredMat.AlbedoTextureHash);
        Assert.Equal(new Vector2(3f, 4f), restoredMat.Tiling);
        Assert.Equal(new Vector2(0.5f, 0.5f), restoredMat.Offset);
        Assert.Equal(0.7f, restoredMat.Metallic);
        Assert.Equal(0.3f, restoredMat.Roughness);
    }

    [Fact]
    public void PrimitiveMeshGenerator_VertexFormat_HasExpectedStride()
    {
        var cube = PrimitiveMeshGenerator.GenerateCube();
        var quad = PrimitiveMeshGenerator.GenerateQuad();
        var plane = PrimitiveMeshGenerator.GeneratePlane();
        var sphere = PrimitiveMeshGenerator.GenerateSphere();

        // 8 floats por vértice: Pos(3) + Normal(3) + UV(2)
        Assert.Equal(0, cube.Vertices.Length % 8);
        Assert.Equal(0, quad.Vertices.Length % 8);
        Assert.Equal(0, plane.Vertices.Length % 8);
        Assert.Equal(0, sphere.Vertices.Length % 8);

        Assert.True(cube.BoundingRadius > 0);
        Assert.True(quad.BoundingRadius > 0);
        Assert.True(plane.BoundingRadius > 0);
        Assert.True(sphere.BoundingRadius > 0);
    }
}
