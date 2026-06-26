namespace ERus.Engine.ECS;

public enum PrimitiveMeshType
{
    None,
    Cube,
    Sphere,
    Plane,
    Capsule,
    Cylinder,
    Quad
}

public struct MeshComponent : IComponent
{
    public PrimitiveMeshType Type;
    public System.Guid AssetGuid;
    public string? AssetHash;
}
