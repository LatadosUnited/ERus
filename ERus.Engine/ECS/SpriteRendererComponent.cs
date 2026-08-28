using System;
using System.Numerics;

namespace ERus.Engine.ECS;

public struct SpriteRendererComponent : IComponent
{
    public Vector4 Color { get; set; } = Vector4.One;
    public Guid SpriteGuid { get; set; } = Guid.Empty;
    public string? SpriteHash { get; set; } = null;
    public bool FlipX { get; set; } = false;
    public bool FlipY { get; set; } = false;
    public int SortingOrder { get; set; } = 0;

    public SpriteRendererComponent()
    {
        Color = Vector4.One;
        SpriteGuid = Guid.Empty;
        SpriteHash = null;
        FlipX = false;
        FlipY = false;
        SortingOrder = 0;
    }
}
