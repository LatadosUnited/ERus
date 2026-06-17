using Silk.NET.Maths;

namespace ERus.Engine.ECS;

public struct TransformComponent : IComponent
{
    public Vector3D<float> Position = Vector3D<float>.Zero;
    public Vector3D<float> Rotation = Vector3D<float>.Zero;
    public Vector3D<float> Scale = Vector3D<float>.One;

    public TransformComponent() {}
}
