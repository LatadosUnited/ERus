using System;
using System.Numerics;
using Silk.NET.Maths;

namespace ERus.Engine.Graphics;

/// <summary>
/// Conversão de um <see cref="ERus.Engine.ECS.TransformComponent"/> para matriz de modelo.
/// A rotação do componente é armazenada em graus, por eixo (Euler XYZ).
/// </summary>
public static class TransformMath
{
    private const float DegreesToRadians = MathF.PI / 180f;

    public static Matrix4x4 RotationMatrix(Vector3D<float> eulerDegrees)
        => Matrix4x4.CreateRotationX(eulerDegrees.X * DegreesToRadians)
         * Matrix4x4.CreateRotationY(eulerDegrees.Y * DegreesToRadians)
         * Matrix4x4.CreateRotationZ(eulerDegrees.Z * DegreesToRadians);

    public static Matrix4x4 ModelMatrix(Vector3D<float> position, Vector3D<float> rotationDegrees, Vector3D<float> scale)
        => Matrix4x4.CreateScale(scale.X, scale.Y, scale.Z)
         * RotationMatrix(rotationDegrees)
         * Matrix4x4.CreateTranslation(position.X, position.Y, position.Z);
}
