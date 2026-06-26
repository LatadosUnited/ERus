using Silk.NET.Maths;

namespace ERus.Engine.ECS;

[System.Flags]
public enum RigidbodyConstraints
{
    None = 0,
    FreezePositionX = 1 << 0,
    FreezePositionY = 1 << 1,
    FreezePositionZ = 1 << 2,
    FreezePosition = FreezePositionX | FreezePositionY | FreezePositionZ,
    FreezeRotationX = 1 << 3,
    FreezeRotationY = 1 << 4,
    FreezeRotationZ = 1 << 5,
    FreezeRotation = FreezeRotationX | FreezeRotationY | FreezeRotationZ,
    FreezeAll = FreezePosition | FreezeRotation
}

public struct RigidBodyComponent : IComponent
{
    public float Mass { get; set; }
    public float LinearDrag { get; set; }
    public float AngularDrag { get; set; }
    public bool UseGravity { get; set; }
    public bool IsKinematic { get; set; }
    
    [NonSerializedComponent]
    public Vector3D<float> LinearVelocity { get; set; }
    [NonSerializedComponent]
    public Vector3D<float> AngularVelocity { get; set; }

    public RigidbodyConstraints Constraints { get; set; }

    // Referência interna para o corpo rígido na biblioteca de física (Jitter2.Dynamics.RigidBody)
    [NonSerializedComponent]
    public object? InternalBody { get; set; }

    public RigidBodyComponent()
    {
        Mass = 1.0f;
        LinearDrag = 0.0f;
        AngularDrag = 0.05f;
        UseGravity = true;
        IsKinematic = false;
        
        LinearVelocity = Vector3D<float>.Zero;
        AngularVelocity = Vector3D<float>.Zero;
        
        Constraints = RigidbodyConstraints.None;
        
        InternalBody = null;
    }
}
