using System;
using System.Numerics;
using ImGuiNET;
using ERus.Editor.EditorUI.Panels;

namespace ERus.Editor.EditorUI;

/// <summary>
/// Encapsula toda a renderização visual do Gizmo via ImGui DrawList.
/// Responsável por desenhar setas, anéis e cubos sobre a viewport 2D.
/// </summary>
public static class GizmoRenderer
{
    /// <summary>
    /// Desenha o gizmo de translação: linhas + setas triangulares.
    /// </summary>
    public static void DrawTranslateGizmo(
        ImDrawListPtr drawList, Vector3[] axes, Vector3 entityPos,
        float gizmoSize, Matrix4x4 viewProj, Vector2 cursorPos, Vector2 viewportSize,
        int activeAxis, int hoveredAxis)
    {
        for (int i = 0; i < 3; i++)
        {
            var pEnd = entityPos + axes[i] * gizmoSize;
            var sPos = GizmoMath.WorldToScreen(entityPos, viewProj, cursorPos, viewportSize);
            var sEnd = GizmoMath.WorldToScreen(pEnd, viewProj, cursorPos, viewportSize);

            bool isActiveOrHovered = (activeAxis == i || (activeAxis == -1 && hoveredAxis == i));
            float lineThickness = isActiveOrHovered ? 4.0f : 3.0f;
            uint color = GetU32(GetAxisColor(i, activeAxis, hoveredAxis));

            drawList.AddLine(sPos, sEnd, color, lineThickness);

            // Ponta: Seta (Triângulo Preenchido)
            var dir = Vector2.Normalize(sEnd - sPos);
            if (float.IsNaN(dir.X)) dir = new Vector2(0, -1);
            var perp = new Vector2(-dir.Y, dir.X);

            float arrowLen = isActiveOrHovered ? 18.0f : 14.0f;
            float arrowWidth = isActiveOrHovered ? 8.0f : 6.0f;
            var baseCenter = sEnd - dir * arrowLen;
            var p1 = baseCenter + perp * arrowWidth;
            var p2 = baseCenter - perp * arrowWidth;
            drawList.AddTriangleFilled(sEnd, p1, p2, color);
        }
    }

    /// <summary>
    /// Desenha o gizmo de escala: linhas + cubos nas pontas + handle central.
    /// </summary>
    public static void DrawScaleGizmo(
        ImDrawListPtr drawList, Vector3[] axes, Vector3 entityPos,
        float gizmoSize, Matrix4x4 viewProj, Vector2 cursorPos, Vector2 viewportSize,
        int activeAxis, int hoveredAxis)
    {
        // Handle central (escala uniforme)
        var centerScreen = GizmoMath.WorldToScreen(entityPos, viewProj, cursorPos, viewportSize);
        bool isCenterActive = activeAxis == 3;
        bool isCenterHovered = (activeAxis == -1 && hoveredAxis == 3);
        bool isCenterHighlighted = isCenterActive || isCenterHovered;

        float centerSize = isCenterHighlighted ? 8.0f : 6.0f;
        uint centerColor = GetU32(new Vector4(1.0f, 0.9f, 0.2f, isCenterHighlighted ? 1.0f : 0.8f)); // Amarelo
        drawList.AddRectFilled(
            centerScreen + new Vector2(-centerSize, -centerSize),
            centerScreen + new Vector2(centerSize, centerSize),
            centerColor);

        // Eixos individuais
        for (int i = 0; i < 3; i++)
        {
            var pEnd = entityPos + axes[i] * gizmoSize;
            var sPos = GizmoMath.WorldToScreen(entityPos, viewProj, cursorPos, viewportSize);
            var sEnd = GizmoMath.WorldToScreen(pEnd, viewProj, cursorPos, viewportSize);

            bool isActiveOrHovered = (activeAxis == i || (activeAxis == -1 && hoveredAxis == i));
            float lineThickness = isActiveOrHovered ? 4.0f : 3.0f;
            uint color = GetU32(GetAxisColor(i, activeAxis, hoveredAxis));

            drawList.AddLine(sPos, sEnd, color, lineThickness);

            // Ponta: Cubo (Quadrado Preenchido)
            float boxSize = isActiveOrHovered ? 6.0f : 5.0f;
            var bp1 = sEnd + new Vector2(-boxSize, -boxSize);
            var bp2 = sEnd + new Vector2(boxSize, boxSize);
            drawList.AddRectFilled(bp1, bp2, color);
        }
    }

    /// <summary>
    /// Desenha o gizmo de rotação: anéis circulares com backface fading.
    /// </summary>
    public static void DrawRotateGizmo(
        ImDrawListPtr drawList, Vector3[] axes, Vector3 entityPos,
        float gizmoSize, Matrix4x4 viewProj, Vector2 cursorPos, Vector2 viewportSize,
        Vector3 rayOrigin, int activeAxis, int hoveredAxis)
    {
        for (int i = 0; i < 3; i++)
        {
            int segments = 48;
            var p0 = Vector2.Zero;

            bool isActiveOrHovered = (activeAxis == i || (activeAxis == -1 && hoveredAxis == i));
            float thickness = isActiveOrHovered ? 3.5f : 2.0f;

            for (int j = 0; j <= segments; j++)
            {
                float a = (j / (float)segments) * MathF.PI * 2.0f;

                var tangent = GizmoMath.GetStableTangent(axes[i]);
                var bitangent = Vector3.Normalize(Vector3.Cross(axes[i], tangent));

                Vector3 wp = entityPos + (tangent * MathF.Cos(a) + bitangent * MathF.Sin(a)) * gizmoSize;

                // Fading para backface (ilusão de 3D)
                float dot = Vector3.Dot(
                    Vector3.Normalize(wp - entityPos),
                    Vector3.Normalize(rayOrigin - wp));
                float alphaMult = dot < 0 ? 0.15f : 1.0f;

                uint segmentColor = GetU32(GetAxisColor(i, activeAxis, hoveredAxis, alphaMult));

                var p1 = GizmoMath.WorldToScreen(wp, viewProj, cursorPos, viewportSize);
                if (j > 0 && p0.X != -1000 && p1.X != -1000)
                    drawList.AddLine(p0, p1, segmentColor, thickness);
                p0 = p1;
            }
        }
    }

    /// <summary>
    /// Desenha indicador visual de snapping (linhas tracejadas).
    /// </summary>
    public static void DrawSnapIndicator(
        ImDrawListPtr drawList, Vector2 cursorPos, Vector2 viewportSize, bool isSnapping)
    {
        if (!isSnapping) return;

        uint color = GetU32(new Vector4(1.0f, 1.0f, 0.0f, 0.4f));
        var textPos = cursorPos + new Vector2(8, viewportSize.Y - 20);
        drawList.AddText(textPos, color, "SNAP");
    }

    /// <summary>
    /// Retorna cor do eixo com base no estado (active/hovered/normal).
    /// X=Vermelho, Y=Verde, Z=Azul.
    /// </summary>
    public static Vector4 GetAxisColor(int axis, int activeAxis, int hoveredAxis, float alphaMult = 1.0f)
    {
        bool active = activeAxis == axis || (activeAxis == -1 && hoveredAxis == axis);
        float alpha = (active ? 1.0f : 0.7f) * alphaMult;
        float mult = active ? 1.0f : 0.8f;

        if (axis == 0) return new Vector4(1.0f * mult, 0.2f * mult, 0.3f * mult, alpha); // Red
        if (axis == 1) return new Vector4(0.3f * mult, 1.0f * mult, 0.2f * mult, alpha); // Green
        if (axis == 2) return new Vector4(0.2f * mult, 0.3f * mult, 1.0f * mult, alpha); // Blue
        return new Vector4(1, 1, 1, 1);
    }

    /// <summary>
    /// Desenha a caixa delimitadora (bounding box) e a tag do colaborador na Viewport.
    /// </summary>
    public static void DrawCollaboratorSelection(
        ImDrawListPtr drawList, Vector3 entityPos, Vector3 entityScale,
        Matrix4x4 viewProj, Vector2 cursorPos, Vector2 viewportSize,
        string username, Vector4 userColor)
    {
        // Half sizes (mínimo 0.5 para entidades sem malha/pequenas)
        float hx = MathF.Max(0.5f, MathF.Abs(entityScale.X) * 0.5f);
        float hy = MathF.Max(0.5f, MathF.Abs(entityScale.Y) * 0.5f);
        float hz = MathF.Max(0.5f, MathF.Abs(entityScale.Z) * 0.5f);

        Vector3[] corners = new Vector3[8]
        {
            entityPos + new Vector3(-hx, -hy, -hz),
            entityPos + new Vector3( hx, -hy, -hz),
            entityPos + new Vector3( hx,  hy, -hz),
            entityPos + new Vector3(-hx,  hy, -hz),
            entityPos + new Vector3(-hx, -hy,  hz),
            entityPos + new Vector3( hx, -hy,  hz),
            entityPos + new Vector3( hx,  hy,  hz),
            entityPos + new Vector3(-hx,  hy,  hz)
        };

        Vector2[] screenCorners = new Vector2[8];
        for (int i = 0; i < 8; i++)
        {
            screenCorners[i] = GizmoMath.WorldToScreen(corners[i], viewProj, cursorPos, viewportSize);
        }

        uint boxColor = GetU32(new Vector4(userColor.X, userColor.Y, userColor.Z, 0.85f));
        float thickness = 2.0f;

        // 12 arestas do cubo
        int[][] edges = new int[][]
        {
            new[] {0,1}, new[] {1,2}, new[] {2,3}, new[] {3,0},
            new[] {4,5}, new[] {5,6}, new[] {6,7}, new[] {7,4},
            new[] {0,4}, new[] {1,5}, new[] {2,6}, new[] {3,7}
        };

        foreach (var edge in edges)
        {
            var p1 = screenCorners[edge[0]];
            var p2 = screenCorners[edge[1]];
            if (p1.X != -1000 && p2.X != -1000)
            {
                drawList.AddLine(p1, p2, boxColor, thickness);
            }
        }

        // Tag com nome do usuário acima do objeto
        Vector3 topWorld = entityPos + new Vector3(0, hy + 0.3f, 0);
        var tagPos = GizmoMath.WorldToScreen(topWorld, viewProj, cursorPos, viewportSize);
        if (tagPos.X != -1000)
        {
            string label = $" {username} ";
            var textSize = ImGui.CalcTextSize(label);
            var tagMin = tagPos - new Vector2(textSize.X * 0.5f, textSize.Y + 4);
            var tagMax = tagPos + new Vector2(textSize.X * 0.5f, 4);

            uint bgCol = GetU32(new Vector4(0.1f, 0.1f, 0.12f, 0.85f));
            uint borderCol = GetU32(userColor);
            drawList.AddRectFilled(tagMin, tagMax, bgCol, 4.0f);
            drawList.AddRect(tagMin, tagMax, borderCol, 4.0f, ImDrawFlags.None, 1.5f);
            drawList.AddText(tagMin + new Vector2(0, 2), borderCol, label);
        }
    }

    private static uint GetU32(Vector4 v) => ImGui.ColorConvertFloat4ToU32(v);
}


