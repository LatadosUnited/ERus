using Silk.NET.Maths;

namespace ERus.Engine.ECS;

public struct NetworkInterpolationComponent : IComponent
{
    public Vector3D<float> TargetPosition;
    public Vector3D<float> TargetRotation;
    public Vector3D<float> TargetScale;
    
    public bool HasTargetPosition;
    public bool HasTargetRotation;
    public bool HasTargetScale;

    public NetworkInterpolationComponent() 
    {
        TargetPosition = Vector3D<float>.Zero;
        TargetRotation = Vector3D<float>.Zero;
        TargetScale = Vector3D<float>.One;
        HasTargetPosition = false;
        HasTargetRotation = false;
        HasTargetScale = false;
    }
}
