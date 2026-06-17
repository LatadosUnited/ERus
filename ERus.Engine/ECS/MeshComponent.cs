namespace ERus.Engine.ECS;

public enum PrimitiveMeshType
{
    None,
    Cube,
    Sphere,
    Plane
}

public struct MeshComponent : IComponent
{
    public PrimitiveMeshType Type;
    public string? AssetPath;
}
