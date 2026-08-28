using Silk.NET.Maths;

namespace ERus.Engine.ECS;

public struct NetworkInterpolationComponent : IComponent
{
    public Vector3D<float> TargetPosition;
    public Vector3D<float> TargetRotation;
    public Vector3D<float> TargetScale;
    
    public Vector3D<float> Velocity;
    
    public bool HasTargetPosition;
    public bool HasTargetRotation;
    public bool HasTargetScale;

    public float InterpolationSpeed;
    public double LastPacketTime;

    public NetworkInterpolationComponent() 
    {
        TargetPosition = Vector3D<float>.Zero;
        TargetRotation = Vector3D<float>.Zero;
        TargetScale = Vector3D<float>.One;
        Velocity = Vector3D<float>.Zero;
        HasTargetPosition = false;
        HasTargetRotation = false;
        HasTargetScale = false;
        InterpolationSpeed = 18f;
        LastPacketTime = 0;
    }
}
