using ERus.Engine.Scripting;
using ERus.Engine.ECS;
using Silk.NET.Maths;

/// <summary>
/// Script de exemplo que rotaciona a entidade continuamente ao redor do eixo Y.
/// Para usar: atribua este script a qualquer entidade com TransformComponent via Inspector.
/// </summary>
public class RotateScript : ERusScript
{
    /// <summary>
    /// Velocidade de rotação em graus por segundo.
    /// </summary>
    private float _rotationSpeed = 45.0f;

    public override void Start()
    {
        Log($"RotateScript iniciado! Velocidade: {_rotationSpeed}°/s");
    }

    public override void Update()
    {
        ref var t = ref Transform;
        t.Rotation = new Vector3D<float>(
            t.Rotation.X,
            t.Rotation.Y + _rotationSpeed * (float)DeltaTime,
            t.Rotation.Z
        );
    }

    public override void OnDestroy()
    {
        Log("RotateScript destruído.");
    }
}
