using System.Numerics;
using Silk.NET.OpenGL;
using ERus.Engine.Graphics.Buffers;

namespace ERus.Engine.Graphics.Gizmos;

/// <summary>
/// Geometria de apoio visual do editor: a grade do chão e o wireframe que representa
/// uma entidade com câmera. Não faz parte da cena renderizada no jogo.
/// </summary>
public static class EditorGizmoMeshes
{
    // --- Grade --------------------------------------------------------------

    private const int GridHalfExtent = 1000;
    private const float GridStep = 1.0f;

    private static readonly Vector3 GridLineColor = new(0.4f, 0.4f, 0.4f);
    private static readonly Vector3 GridAxisZColor = new(0.0f, 0.0f, 1.0f);
    private static readonly Vector3 GridAxisXColor = new(1.0f, 0.0f, 0.0f);

    /// <summary>Grade no plano XZ, com os eixos centrais destacados em azul (Z) e vermelho (X).</summary>
    public static LineMesh BuildGrid(GL gl)
    {
        var builder = new LineMeshBuilder();
        float extent = GridHalfExtent * GridStep;

        for (int i = -GridHalfExtent; i <= GridHalfExtent; i++)
        {
            float offset = i * GridStep;
            bool isAxis = i == 0;

            builder.AddLine(
                new Vector3(offset, 0f, -extent),
                new Vector3(offset, 0f, extent),
                isAxis ? GridAxisZColor : GridLineColor);

            builder.AddLine(
                new Vector3(-extent, 0f, offset),
                new Vector3(extent, 0f, offset),
                isAxis ? GridAxisXColor : GridLineColor);
        }

        return new LineMesh(gl, builder.ToArray());
    }

    // --- Ícone de câmera ----------------------------------------------------

    private static readonly Vector3 CameraColor = new(0.9f, 0.9f, 0.2f);
    private static readonly Vector3 BodyMin = new(-0.3f, -0.2f, -0.2f);
    private static readonly Vector3 BodyMax = new(0.3f, 0.2f, 0.3f);

    /// <summary>Caixa do corpo da câmera mais o retângulo do frustum à frente dela.</summary>
    public static LineMesh BuildCameraGizmo(GL gl)
    {
        var builder = new LineMeshBuilder();

        // Cantos traseiros do corpo, reutilizados como origem das arestas do frustum.
        var backBL = new Vector3(BodyMin.X, BodyMin.Y, BodyMin.Z);
        var backBR = new Vector3(BodyMax.X, BodyMin.Y, BodyMin.Z);
        var backTR = new Vector3(BodyMax.X, BodyMax.Y, BodyMin.Z);
        var backTL = new Vector3(BodyMin.X, BodyMax.Y, BodyMin.Z);

        var frontBL = new Vector3(BodyMin.X, BodyMin.Y, BodyMax.Z);
        var frontBR = new Vector3(BodyMax.X, BodyMin.Y, BodyMax.Z);
        var frontTR = new Vector3(BodyMax.X, BodyMax.Y, BodyMax.Z);
        var frontTL = new Vector3(BodyMin.X, BodyMax.Y, BodyMax.Z);

        AddQuad(builder, backBL, backBR, backTR, backTL);
        AddQuad(builder, frontBL, frontBR, frontTR, frontTL);

        builder.AddLine(backBL, frontBL, CameraColor);
        builder.AddLine(backBR, frontBR, CameraColor);
        builder.AddLine(backTR, frontTR, CameraColor);
        builder.AddLine(backTL, frontTL, CameraColor);

        // Retângulo do frustum, projetado à frente da câmera (-Z).
        var coneBL = new Vector3(-0.6f, -0.4f, -1.0f);
        var coneBR = new Vector3(0.6f, -0.4f, -1.0f);
        var coneTR = new Vector3(0.6f, 0.4f, -1.0f);
        var coneTL = new Vector3(-0.6f, 0.4f, -1.0f);

        AddQuad(builder, coneBL, coneBR, coneTR, coneTL);

        builder.AddLine(backBL, coneBL, CameraColor);
        builder.AddLine(backBR, coneBR, CameraColor);
        builder.AddLine(backTR, coneTR, CameraColor);
        builder.AddLine(backTL, coneTL, CameraColor);

        return new LineMesh(gl, builder.ToArray());
    }

    private static void AddQuad(LineMeshBuilder builder, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        builder.AddLine(a, b, CameraColor);
        builder.AddLine(b, c, CameraColor);
        builder.AddLine(c, d, CameraColor);
        builder.AddLine(d, a, CameraColor);
    }
}
