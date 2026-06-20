using Silk.NET.Maths;

namespace ERus.Engine.ECS;

public enum ColliderShape
{
    Box,
    Sphere,
    Capsule
}

public struct ColliderComponent : IComponent
{
    public ColliderShape Shape { get; set; }
    
    // Dimensões: Para Box (Tamanho X, Y, Z), Para Sphere (Raio em X), Para Capsule (Raio X, Altura Y)
    public Vector3D<float> Size { get; set; }
    
    public float Friction { get; set; }
    public float Restitution { get; set; } // Elasticidade
    
    public bool IsTrigger { get; set; }

    public ColliderComponent()
    {
        Shape = ColliderShape.Box;
        Size = new Vector3D<float>(1, 1, 1);
        Friction = 0.5f;
        Restitution = 0.1f;
        IsTrigger = false;
    }
}
