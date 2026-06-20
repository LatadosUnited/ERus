using Silk.NET.Maths;

namespace ERus.Engine.ECS;

/// <summary>
/// Estrutura de dados que descreve um evento de colisão ou trigger.
/// </summary>
public struct CollisionEvent
{
    public int EntityA { get; set; }
    public int EntityB { get; set; }
    
    public bool IsTriggerEvent { get; set; }
    
    public Vector3D<float> ImpactPoint { get; set; }
    public Vector3D<float> Normal { get; set; }
}
