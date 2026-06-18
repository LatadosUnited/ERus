using Silk.NET.Maths;

namespace ERus.Engine.ECS;

public struct TransformComponent : IComponent
{
    public bool IsDirty { get; set; } = false;

    private Vector3D<float> _position = Vector3D<float>.Zero;
    public Vector3D<float> Position
    {
        get => _position;
        set { _position = value; IsDirty = true; }
    }

    private Vector3D<float> _rotation = Vector3D<float>.Zero;
    public Vector3D<float> Rotation
    {
        get => _rotation;
        set { _rotation = value; IsDirty = true; }
    }

    private Vector3D<float> _scale = Vector3D<float>.One;
    public Vector3D<float> Scale
    {
        get => _scale;
        set { _scale = value; IsDirty = true; }
    }

    public TransformComponent() {}
}
