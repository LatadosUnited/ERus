using System;

namespace ERus.Engine.ECS;

/// <summary>
/// Marca uma propriedade ou campo de um IComponent como "somente runtime".
/// O SceneSerializer ignorará este membro ao salvar/carregar cenas.
/// Útil para referências internas de bibliotecas externas (ex: Jitter2 RigidBody).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class NonSerializedComponentAttribute : Attribute
{
}
