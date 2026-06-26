using System;
using System.Numerics;
using ERus.Editor.EditorUI.Panels;

namespace ERus.Editor.EditorUI;

/// <summary>
/// Classe estática com toda a matemática pura do Gizmo.
/// Sem estado, sem side-effects — apenas cálculos.
/// </summary>
public static class GizmoMath
{
    /// <summary>
    /// Converte posição do mouse (relativa ao viewport) em um raio 3D no espaço mundo.
    /// </summary>
    public static (Vector3 origin, Vector3 direction) ScreenToRay(
        Vector2 mousePos, Vector2 viewportSize,
        Matrix4x4 proj, Matrix4x4 view)
    {
        float ndcX = (2.0f * mousePos.X) / viewportSize.X - 1.0f;
        float ndcY = 1.0f - (2.0f * mousePos.Y) / viewportSize.Y;

        var viewProj = view * proj;
        Matrix4x4.Invert(viewProj, out var invViewProj);

        // Near point (z = -1 in NDC for OpenGL-like projection)
        var nearPoint = Vector4.Transform(new Vector4(ndcX, ndcY, -1.0f, 1.0f), invViewProj);
        var pNear = new Vector3(nearPoint.X, nearPoint.Y, nearPoint.Z) / nearPoint.W;

        // Far point (z = 1 in NDC)
        var farPoint = Vector4.Transform(new Vector4(ndcX, ndcY, 1.0f, 1.0f), invViewProj);
        var pFar = new Vector3(farPoint.X, farPoint.Y, farPoint.Z) / farPoint.W;

        var direction = Vector3.Normalize(pFar - pNear);
        
        // Em projeção ortográfica, a origem do raio não é a posição da câmera,
        // mas sim o ponto no plano near. (Ortográfica costuma ter M44 == 1.0)
        bool isOrtho = proj.M44 == 1.0f;
        
        Matrix4x4.Invert(view, out var invView);
        var origin = isOrtho ? pNear : invView.Translation;

        return (origin, direction);
    }

    /// <summary>
    /// Calcula o ponto mais próximo entre um raio e uma linha no espaço 3D.
    /// Retorna o parâmetro tc ao longo da linha e a distância mínima entre os dois.
    /// </summary>
    public static float GetRayLineIntersection(
        Vector3 rayOrigin, Vector3 rayDir,
        Vector3 lineOrigin, Vector3 lineDir,
        out float distToLine)
    {
        var w0 = rayOrigin - lineOrigin;
        float a = Vector3.Dot(rayDir, rayDir);
        float b = Vector3.Dot(rayDir, lineDir);
        float c = Vector3.Dot(lineDir, lineDir);
        float d = Vector3.Dot(rayDir, w0);
        float e = Vector3.Dot(lineDir, w0);
        float denom = a * c - b * b;

        if (denom < 0.0001f)
        {
            distToLine = 9999f;
            return 0f;
        }

        float tc = (a * e - b * d) / denom;
        float sc = (b * e - c * d) / denom;
        var pRay = rayOrigin + rayDir * sc;
        var pLine = lineOrigin + lineDir * tc;
        distToLine = Vector3.Distance(pRay, pLine);
        return tc;
    }

    /// <summary>
    /// Interseção raio-plano. Retorna true se houve interseção e o ponto de hit.
    /// </summary>
    public static bool GetRayPlaneIntersection(
        Vector3 rayOrigin, Vector3 rayDir,
        Vector3 planeNormal, Vector3 planePoint,
        out Vector3 hit)
    {
        hit = Vector3.Zero;
        float denom = Vector3.Dot(planeNormal, rayDir);
        if (MathF.Abs(denom) > 1e-6)
        {
            float t = Vector3.Dot(planePoint - rayOrigin, planeNormal) / denom;
            if (t >= 0)
            {
                hit = rayOrigin + rayDir * t;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Calcula ângulo de rotação a partir de um ponto de hit projetado nos eixos tangente/bitangente.
    /// </summary>
    public static float CalculateAngle(Vector3 center, Vector3 hit, int axis, Vector3[] localAxes)
    {
        var dir = Vector3.Normalize(hit - center);
        if (axis == 0) return MathF.Atan2(Vector3.Dot(dir, localAxes[2]), Vector3.Dot(dir, localAxes[1]));
        if (axis == 1) return MathF.Atan2(Vector3.Dot(dir, localAxes[2]), Vector3.Dot(dir, localAxes[0]));
        if (axis == 2) return MathF.Atan2(Vector3.Dot(dir, localAxes[1]), Vector3.Dot(dir, localAxes[0]));
        return 0;
    }

    /// <summary>
    /// Tangente estável para evitar "Steering Wheel effect" nos anéis de rotação.
    /// </summary>
    public static Vector3 GetStableTangent(Vector3 normal)
    {
        var t = Vector3.Cross(normal, Vector3.UnitY);
        if (t.LengthSquared() < 0.001f)
            t = Vector3.Cross(normal, Vector3.UnitX);
        return Vector3.Normalize(t);
    }

    /// <summary>
    /// Extrai ângulos Euler (em graus) de uma matriz de rotação.
    /// Usa decomposição robusta com fallback para gimbal lock.
    /// </summary>
    public static Vector3 ExtractEuler(Matrix4x4 m)
    {
        Vector3 euler;

        // Clamp para evitar NaN no Asin
        float m13Clamped = MathF.Max(-1.0f, MathF.Min(1.0f, m.M13));

        if (m13Clamped < 0.999f)
        {
            if (m13Clamped > -0.999f)
            {
                euler.Y = MathF.Asin(-m13Clamped);
                euler.X = MathF.Atan2(m.M23, m.M33);
                euler.Z = MathF.Atan2(m.M12, m.M11);
            }
            else
            {
                // Gimbal lock: M13 ≈ -1
                euler.Y = MathF.PI / 2.0f;
                euler.X = -MathF.Atan2(-m.M21, m.M22);
                euler.Z = 0;
            }
        }
        else
        {
            // Gimbal lock: M13 ≈ 1
            euler.Y = -MathF.PI / 2.0f;
            euler.X = MathF.Atan2(-m.M21, m.M22);
            euler.Z = 0;
        }

        return euler * (180.0f / MathF.PI);
    }

    /// <summary>
    /// Constrói matriz de rotação a partir de ângulos Euler em graus.
    /// </summary>
    public static Matrix4x4 BuildRotationMatrix(Vector3 eulerDegrees)
    {
        var rx = Matrix4x4.CreateRotationX(eulerDegrees.X * MathF.PI / 180f);
        var ry = Matrix4x4.CreateRotationY(eulerDegrees.Y * MathF.PI / 180f);
        var rz = Matrix4x4.CreateRotationZ(eulerDegrees.Z * MathF.PI / 180f);
        return rx * ry * rz;
    }

    /// <summary>
    /// Calcula os 3 eixos do gizmo, considerando espaço local/global.
    /// Scale sempre usa espaço local.
    /// </summary>
    public static Vector3[] ComputeAxes(Vector3 eulerRotation, bool isLocalSpace, GizmoMode mode)
    {
        var matrixToUse = (isLocalSpace || mode == GizmoMode.Scale)
            ? BuildRotationMatrix(eulerRotation)
            : Matrix4x4.Identity;

        return new[]
        {
            Vector3.Transform(Vector3.UnitX, matrixToUse),
            Vector3.Transform(Vector3.UnitY, matrixToUse),
            Vector3.Transform(Vector3.UnitZ, matrixToUse)
        };
    }

    /// <summary>
    /// Snap de valor para incremento fixo.
    /// </summary>
    public static float SnapValue(float value, float snapSize)
    {
        if (snapSize <= 0) return value;
        return MathF.Round(value / snapSize) * snapSize;
    }

    /// <summary>
    /// Snap de vetor para incremento fixo por componente.
    /// </summary>
    public static Vector3 SnapVector(Vector3 value, float snapSize)
    {
        if (snapSize <= 0) return value;
        return new Vector3(
            SnapValue(value.X, snapSize),
            SnapValue(value.Y, snapSize),
            SnapValue(value.Z, snapSize));
    }

    /// <summary>
    /// Interseção Ray-AABB (Axis-Aligned Bounding Box).
    /// Retorna (intersected, distance).
    /// </summary>
    public static (bool hit, float distance) RayAABBIntersection(
        Vector3 rayOrigin, Vector3 rayDir,
        Vector3 boxMin, Vector3 boxMax)
    {
        float tMin = 0.0f;
        float tMax = float.MaxValue;

        for (int i = 0; i < 3; i++)
        {
            float o = i == 0 ? rayOrigin.X : (i == 1 ? rayOrigin.Y : rayOrigin.Z);
            float d = i == 0 ? rayDir.X : (i == 1 ? rayDir.Y : rayDir.Z);
            float bMin = i == 0 ? boxMin.X : (i == 1 ? boxMin.Y : boxMin.Z);
            float bMax = i == 0 ? boxMax.X : (i == 1 ? boxMax.Y : boxMax.Z);

            if (MathF.Abs(d) < 1e-6)
            {
                if (o < bMin || o > bMax) return (false, 0);
            }
            else
            {
                float t1 = (bMin - o) / d;
                float t2 = (bMax - o) / d;
                if (t1 > t2) { (t1, t2) = (t2, t1); }
                if (t1 > tMin) tMin = t1;
                if (t2 < tMax) tMax = t2;
                if (tMin > tMax) return (false, 0);
            }
        }

        return (tMin > 0, tMin);
    }

    /// <summary>
    /// Interseção Ray-OBB (Oriented Bounding Box).
    /// Transforma o raio para o espaço local da entidade antes de testar AABB.
    /// </summary>
    public static (bool hit, float distance) RayOBBIntersection(
        Vector3 rayOrigin, Vector3 rayDir,
        Vector3 center, Vector3 halfExtents, Matrix4x4 rotMatrix)
    {
        // Construir a inversa da transform (só rotação + translação)
        Matrix4x4.Invert(rotMatrix, out var invRot);

        // Transformar raio para espaço local
        var localOrigin = Vector3.Transform(rayOrigin - center, invRot);
        var localDir = Vector3.TransformNormal(rayDir, invRot);

        // Testar AABB no espaço local
        var boxMin = -halfExtents;
        var boxMax = halfExtents;

        return RayAABBIntersection(localOrigin, localDir, boxMin, boxMax);
    }

    /// <summary>
    /// Projeta um ponto 3D para coordenadas 2D de tela.
    /// </summary>
    public static Vector2 WorldToScreen(
        Vector3 worldPos, Matrix4x4 viewProj,
        Vector2 cursorPos, Vector2 viewportSize)
    {
        var clip = Vector4.Transform(new Vector4(worldPos, 1.0f), viewProj);
        if (clip.W < 0.0001f) return new Vector2(-1000, -1000);
        var ndc = new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);
        return cursorPos + new Vector2(
            (ndc.X + 1.0f) * 0.5f * viewportSize.X,
            (1.0f - ndc.Y) * 0.5f * viewportSize.Y);
    }
}


