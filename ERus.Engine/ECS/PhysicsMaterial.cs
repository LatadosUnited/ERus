using System;

namespace ERus.Engine.ECS;

public enum CombineMode
{
    Average,
    Minimum,
    Multiply,
    Maximum
}

public class PhysicsMaterial
{
    public float DynamicFriction { get; set; } = 0.6f;
    public float StaticFriction { get; set; } = 0.6f;
    public float Bounciness { get; set; } = 0.0f;
    public CombineMode FrictionCombine { get; set; } = CombineMode.Average;
    public CombineMode BounceCombine { get; set; } = CombineMode.Average;
}
