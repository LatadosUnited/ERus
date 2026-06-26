using System.Numerics;

namespace ERus.Engine.Math;

public struct Frustum
{
    // A estrutura contém os 6 planos: [0]=Left, [1]=Right, [2]=Bottom, [3]=Top, [4]=Near, [5]=Far
    private readonly Plane[] _planes;

    public Frustum(Matrix4x4 viewProjection)
    {
        _planes = new Plane[6];

        // Column 4 + Column 1
        _planes[0] = Plane.Normalize(new Plane(
            viewProjection.M14 + viewProjection.M11,
            viewProjection.M24 + viewProjection.M21,
            viewProjection.M34 + viewProjection.M31,
            viewProjection.M44 + viewProjection.M41
        ));

        // Column 4 - Column 1
        _planes[1] = Plane.Normalize(new Plane(
            viewProjection.M14 - viewProjection.M11,
            viewProjection.M24 - viewProjection.M21,
            viewProjection.M34 - viewProjection.M31,
            viewProjection.M44 - viewProjection.M41
        ));

        // Column 4 + Column 2
        _planes[2] = Plane.Normalize(new Plane(
            viewProjection.M14 + viewProjection.M12,
            viewProjection.M24 + viewProjection.M22,
            viewProjection.M34 + viewProjection.M32,
            viewProjection.M44 + viewProjection.M42
        ));

        // Column 4 - Column 2
        _planes[3] = Plane.Normalize(new Plane(
            viewProjection.M14 - viewProjection.M12,
            viewProjection.M24 - viewProjection.M22,
            viewProjection.M34 - viewProjection.M32,
            viewProjection.M44 - viewProjection.M42
        ));

        // Column 4 + Column 3 (Near, em OpenGL padrão pode ser apenas a coluna 3, dependendo do NDC. Vamos cobrir [-1, 1] e [0, 1] com heurística).
        _planes[4] = Plane.Normalize(new Plane(
            viewProjection.M14 + viewProjection.M13,
            viewProjection.M24 + viewProjection.M23,
            viewProjection.M34 + viewProjection.M33,
            viewProjection.M44 + viewProjection.M43
        ));

        // Column 4 - Column 3
        _planes[5] = Plane.Normalize(new Plane(
            viewProjection.M14 - viewProjection.M13,
            viewProjection.M24 - viewProjection.M23,
            viewProjection.M34 - viewProjection.M33,
            viewProjection.M44 - viewProjection.M43
        ));
    }

    /// <summary>
    /// Verifica se a esfera se intersecta ou está completamente dentro do Frustum.
    /// Operação O(1) muito rápida para cada entidade.
    /// </summary>
    public bool IntersectsSphere(Vector3 center, float radius)
    {
        for (int i = 0; i < 6; i++)
        {
            // A distância de um ponto para o plano no System.Numerics.Plane
            // Plane.DotCoordinate(plane, point) retorna a distância assinada se o plano for normalizado.
            float distance = Plane.DotCoordinate(_planes[i], center);

            // Se a distância for menor que o -raio, a esfera inteira está FORA desse plano.
            if (distance < -radius)
            {
                return false;
            }
        }

        return true;
    }
}
