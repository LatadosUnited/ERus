using System;
using Silk.NET.Maths;

namespace ERus.Engine.ECS;

public struct BoxColliderComponent : IComponent
{
    public Vector3D<float> Size { get; set; }
    public Vector3D<float> Center { get; set; }
    public bool IsTrigger { get; set; }
    public Guid PhysicsMaterialId { get; set; }

    public BoxColliderComponent()
    {
        Size = new Vector3D<float>(1, 1, 1);
        Center = Vector3D<float>.Zero;
        IsTrigger = false;
        PhysicsMaterialId = Guid.Empty;
    }
}

public struct SphereColliderComponent : IComponent
{
    public float Radius { get; set; }
    public Vector3D<float> Center { get; set; }
    public bool IsTrigger { get; set; }
    public Guid PhysicsMaterialId { get; set; }

    public SphereColliderComponent()
    {
        Radius = 0.5f;
        Center = Vector3D<float>.Zero;
        IsTrigger = false;
        PhysicsMaterialId = Guid.Empty;
    }
}

public struct CapsuleColliderComponent : IComponent
{
    public float Radius { get; set; }
    public float Height { get; set; }
    public Vector3D<float> Center { get; set; }
    public bool IsTrigger { get; set; }
    public Guid PhysicsMaterialId { get; set; }

    public CapsuleColliderComponent()
    {
        Radius = 0.5f;
        Height = 1.0f; // This usually represents the cylinder part, so total height = Height + 2 * Radius
        Center = Vector3D<float>.Zero;
        IsTrigger = false;
        PhysicsMaterialId = Guid.Empty;
    }
}

public struct CylinderColliderComponent : IComponent
{
    public float Radius { get; set; }
    public float Height { get; set; }
    public Vector3D<float> Center { get; set; }
    public bool IsTrigger { get; set; }
    public Guid PhysicsMaterialId { get; set; }

    public CylinderColliderComponent()
    {
        Radius = 0.5f;
        Height = 1.0f;
        Center = Vector3D<float>.Zero;
        IsTrigger = false;
        PhysicsMaterialId = Guid.Empty;
    }
}

public struct MeshColliderComponent : IComponent
{
    public Guid AssetGuid { get; set; }
    public bool IsConvex { get; set; }
    public Vector3D<float> Center { get; set; }
    public bool IsTrigger { get; set; }
    public Guid PhysicsMaterialId { get; set; }

    public MeshColliderComponent()
    {
        AssetGuid = Guid.Empty;
        IsConvex = true;
        Center = Vector3D<float>.Zero;
        IsTrigger = false;
        PhysicsMaterialId = Guid.Empty;
    }
}
