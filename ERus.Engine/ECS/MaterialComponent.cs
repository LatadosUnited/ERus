using System;
using System.Numerics;

namespace ERus.Engine.ECS;

public struct MaterialComponent : IComponent
{
    public Vector4 ColorTint { get; set; } = Vector4.One;
    public Guid AlbedoTextureGuid { get; set; } = Guid.Empty;
    public string? AlbedoTextureHash { get; set; } = null;
    public Vector2 Tiling { get; set; } = Vector2.One;
    public Vector2 Offset { get; set; } = Vector2.Zero;
    public float Metallic { get; set; } = 0.0f;
    public float Roughness { get; set; } = 0.5f;
    public bool IsTransparent { get; set; } = false;
    public float AlphaCutoff { get; set; } = 0.0f;

    public MaterialComponent()
    {
        ColorTint = Vector4.One;
        AlbedoTextureGuid = Guid.Empty;
        AlbedoTextureHash = null;
        Tiling = Vector2.One;
        Offset = Vector2.Zero;
        Metallic = 0.0f;
        Roughness = 0.5f;
        IsTransparent = false;
        AlphaCutoff = 0.0f;
    }
}
