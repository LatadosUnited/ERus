using Silk.NET.Maths;

namespace ERus.Engine.ECS;

public struct RigidBodyComponent : IComponent
{
    public float Mass { get; set; }
    public bool IsKinematic { get; set; }
    
    public Vector3D<float> LinearVelocity { get; set; }
    public Vector3D<float> AngularVelocity { get; set; }

    // Travamento de Eixos (Constraints)
    public bool FreezePositionX { get; set; }
    public bool FreezePositionY { get; set; }
    public bool FreezePositionZ { get; set; }
    public bool FreezeRotationX { get; set; }
    public bool FreezeRotationY { get; set; }
    public bool FreezeRotationZ { get; set; }

    // Referência interna para o corpo rígido na biblioteca de física (Jitter2.Dynamics.RigidBody)
    public object? InternalBody { get; set; }

    public RigidBodyComponent()
    {
        Mass = 1.0f;
        IsKinematic = false;
        LinearVelocity = Vector3D<float>.Zero;
        AngularVelocity = Vector3D<float>.Zero;
        
        FreezePositionX = false;
        FreezePositionY = false;
        FreezePositionZ = false;
        FreezeRotationX = false;
        FreezeRotationY = false;
        FreezeRotationZ = false;
        
        InternalBody = null;
    }
}
