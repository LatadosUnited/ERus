using System;
using System.Numerics;
using Silk.NET.Maths;
using ERus.Editor.EditorUI.Panels;

namespace ERus.Editor.EditorUI;

/// <summary>
/// Configurações de snapping para o Gizmo.
/// </summary>
public struct GizmoSnapSettings
{
    public bool Enabled;
    public float TranslateSnap;
    public float RotateSnap;
    public float ScaleSnap;

    public static GizmoSnapSettings Default => new()
    {
        Enabled = false,
        TranslateSnap = 0.5f,
        RotateSnap = 15.0f,
        ScaleSnap = 0.1f
    };
}

/// <summary>
/// Gerencia todo o estado de interação do Gizmo: hover, drag, e transformações.
/// Classe com estado (não estática) — uma instância por SceneViewWindow.
/// </summary>
public class GizmoInteraction
{
    // Estado de interação
    public int ActiveAxis { get; private set; } = -1;
    public int HoveredAxis { get; private set; } = -1;
    public bool IsActive => ActiveAxis != -1;

    // Estado de drag armazenado
    private Vector3 _dragStartEntityPos;
    private Vector3 _dragStartEntityRot;
    private Vector3 _dragStartEntityScale;
    private float _dragStartMouseTc;
    private float _lastDragAngle;
    private Vector3[] _dragStartAxes = new Vector3[3];

    // Para o Undo: posição no momento do clique
    private Vector3D<float> _undoStartPos;
    private Vector3D<float> _undoStartRot;
    private Vector3D<float> _undoStartScale;

    // Snapping
    public GizmoSnapSettings SnapSettings = GizmoSnapSettings.Default;

    /// <summary>
    /// Atualiza o estado de hover (qual eixo está sob o cursor).
    /// Deve ser chamado todo frame ANTES de verificar cliques.
    /// </summary>
    public void UpdateHover(
        Vector3 rayOrigin, Vector3 rayDir,
        Vector3 entityPos, Vector3[] axes,
        float gizmoSize, float clickFatness,
        GizmoMode mode)
    {
        HoveredAxis = -1;

        if (IsActive) return; // Não muda hover durante drag

        if (mode == GizmoMode.Translate || mode == GizmoMode.Scale)
        {
            // Testar handle central (escala uniforme) primeiro
            if (mode == GizmoMode.Scale)
            {
                float distToCenter = Vector3.Distance(
                    rayOrigin + rayDir * Vector3.Dot(entityPos - rayOrigin, rayDir),
                    entityPos);
                if (distToCenter < clickFatness * 1.5f)
                {
                    HoveredAxis = 3; // 3 = centro (uniforme)
                    return;
                }
            }

            float bestDist = clickFatness;
            for (int i = 0; i < 3; i++)
            {
                float tc = GizmoMath.GetRayLineIntersection(
                    rayOrigin, rayDir, entityPos, axes[i], out float distToLine);
                if (tc > 0.0f && tc < gizmoSize && distToLine < bestDist)
                {
                    bestDist = distToLine;
                    HoveredAxis = i;
                }
            }
        }
        else if (mode == GizmoMode.Rotate)
        {
            for (int i = 0; i < 3; i++)
            {
                if (GizmoMath.GetRayPlaneIntersection(rayOrigin, rayDir, axes[i], entityPos, out var hit))
                {
                    float distFromCenter = Vector3.Distance(hit, entityPos);
                    if (MathF.Abs(distFromCenter - gizmoSize) < gizmoSize * 0.15f)
                    {
                        HoveredAxis = i;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Inicia uma operação de drag. Armazena estado inicial para cálculo de deltas.
    /// </summary>
    public void BeginDrag(
        Vector3 entityPos, Vector3 entityRot, Vector3 entityScale,
        Vector3[] axes, Vector3 rayOrigin, Vector3 rayDir,
        GizmoMode mode, ERus.Engine.ECS.TransformComponent currentTransform)
    {
        ActiveAxis = HoveredAxis;
        _dragStartEntityPos = entityPos;
        _dragStartEntityRot = entityRot;
        _dragStartEntityScale = entityScale;
        _dragStartAxes[0] = axes[0];
        _dragStartAxes[1] = axes[1];
        _dragStartAxes[2] = axes[2];

        // Salvar para undo
        _undoStartPos = currentTransform.Position;
        _undoStartRot = currentTransform.Rotation;
        _undoStartScale = currentTransform.Scale;

        if (mode == GizmoMode.Translate || mode == GizmoMode.Scale)
        {
            if (ActiveAxis < 3) // Eixo individual
            {
                _dragStartMouseTc = GizmoMath.GetRayLineIntersection(
                    rayOrigin, rayDir, entityPos, axes[ActiveAxis], out _);
            }
            else // Centro (uniforme)
            {
                _dragStartMouseTc = GizmoMath.GetRayLineIntersection(
                    rayOrigin, rayDir, entityPos, Vector3.UnitY, out _);
            }
        }
        else if (mode == GizmoMode.Rotate)
        {
            if (GizmoMath.GetRayPlaneIntersection(
                rayOrigin, rayDir, _dragStartAxes[ActiveAxis], entityPos, out var hit))
            {
                _lastDragAngle = GizmoMath.CalculateAngle(entityPos, hit, ActiveAxis, _dragStartAxes);
            }
        }
    }

    /// <summary>
    /// Atualiza o drag em andamento. Modifica o transform da entidade diretamente.
    /// Retorna true se houve mudança.
    /// </summary>
    public bool UpdateDrag(
        Vector3 rayOrigin, Vector3 rayDir,
        ref ERus.Engine.ECS.TransformComponent transform,
        Vector3[] axes, GizmoMode mode, bool snapping)
    {
        if (!IsActive) return false;

        var snap = snapping ? SnapSettings : new GizmoSnapSettings { Enabled = false };

        if (mode == GizmoMode.Translate)
        {
            return ApplyTranslation(rayOrigin, rayDir, ref transform, axes, snap);
        }
        else if (mode == GizmoMode.Scale)
        {
            return ApplyScale(rayOrigin, rayDir, ref transform, axes, snap);
        }
        else if (mode == GizmoMode.Rotate)
        {
            return ApplyRotation(rayOrigin, rayDir, ref transform, snap);
        }

        return false;
    }

    /// <summary>
    /// Finaliza o drag. Retorna os dados necessários para o TransformCommand.
    /// </summary>
    public (Vector3D<float> oldPos, Vector3D<float> oldRot, Vector3D<float> oldScale,
            Vector3D<float> newPos, Vector3D<float> newRot, Vector3D<float> newScale)
        EndDrag(ERus.Engine.ECS.TransformComponent currentTransform)
    {
        var result = (
            _undoStartPos, _undoStartRot, _undoStartScale,
            currentTransform.Position, currentTransform.Rotation, currentTransform.Scale
        );
        ActiveAxis = -1;
        return result;
    }

    /// <summary>
    /// Cancela o drag sem aplicar mudança.
    /// </summary>
    public void CancelDrag()
    {
        ActiveAxis = -1;
    }

    // ---- Métodos privados de aplicação ----

    private bool ApplyTranslation(
        Vector3 rayOrigin, Vector3 rayDir,
        ref ERus.Engine.ECS.TransformComponent transform,
        Vector3[] axes, GizmoSnapSettings snap)
    {
        int axis = ActiveAxis;
        var axisDir = axes[axis];

        float currentTc = GizmoMath.GetRayLineIntersection(
            rayOrigin, rayDir, _dragStartEntityPos, axisDir, out _);
        float delta = currentTc - _dragStartMouseTc;

        var newPos = _dragStartEntityPos + axisDir * delta;

        if (snap.Enabled)
            newPos = GizmoMath.SnapVector(newPos, snap.TranslateSnap);

        transform.Position = new Vector3D<float>(newPos.X, newPos.Y, newPos.Z);
        return true;
    }

    private bool ApplyScale(
        Vector3 rayOrigin, Vector3 rayDir,
        ref ERus.Engine.ECS.TransformComponent transform,
        Vector3[] axes, GizmoSnapSettings snap)
    {
        float delta;

        if (ActiveAxis == 3)
        {
            // Escala uniforme
            float currentTc = GizmoMath.GetRayLineIntersection(
                rayOrigin, rayDir, _dragStartEntityPos, Vector3.UnitY, out _);
            delta = currentTc - _dragStartMouseTc;

            if (snap.Enabled)
                delta = GizmoMath.SnapValue(delta, snap.ScaleSnap);

            var newScale = _dragStartEntityScale + Vector3.One * delta;
            transform.Scale = new Vector3D<float>(newScale.X, newScale.Y, newScale.Z);
        }
        else
        {
            // Escala por eixo
            float currentTc = GizmoMath.GetRayLineIntersection(
                rayOrigin, rayDir, _dragStartEntityPos, axes[ActiveAxis], out _);
            delta = currentTc - _dragStartMouseTc;

            if (snap.Enabled)
                delta = GizmoMath.SnapValue(delta, snap.ScaleSnap);

            var newScale = _dragStartEntityScale;
            if (ActiveAxis == 0) newScale += new Vector3(delta, 0, 0);
            if (ActiveAxis == 1) newScale += new Vector3(0, delta, 0);
            if (ActiveAxis == 2) newScale += new Vector3(0, 0, delta);
            transform.Scale = new Vector3D<float>(newScale.X, newScale.Y, newScale.Z);
        }

        return true;
    }

    private bool ApplyRotation(
        Vector3 rayOrigin, Vector3 rayDir,
        ref ERus.Engine.ECS.TransformComponent transform,
        GizmoSnapSettings snap)
    {
        if (!GizmoMath.GetRayPlaneIntersection(
            rayOrigin, rayDir, _dragStartAxes[ActiveAxis], _dragStartEntityPos, out var hit))
            return false;

        float currentAngle = GizmoMath.CalculateAngle(_dragStartEntityPos, hit, ActiveAxis, _dragStartAxes);
        float deltaAngle = currentAngle - _lastDragAngle;

        // Unwrap para evitar saltos de ±π
        while (deltaAngle > MathF.PI) deltaAngle -= 2 * MathF.PI;
        while (deltaAngle < -MathF.PI) deltaAngle += 2 * MathF.PI;

        if (snap.Enabled)
        {
            float snapRad = snap.RotateSnap * MathF.PI / 180f;
            deltaAngle = GizmoMath.SnapValue(deltaAngle, snapRad);
        }

        var currentRotMatrix = GizmoMath.BuildRotationMatrix(
            new Vector3(transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z));

        var deltaRot = Matrix4x4.CreateFromAxisAngle(
            Vector3.Normalize(_dragStartAxes[ActiveAxis]), deltaAngle);
        var newRotMatrix = currentRotMatrix * deltaRot;

        var newEuler = GizmoMath.ExtractEuler(newRotMatrix);
        transform.Rotation = new Vector3D<float>(newEuler.X, newEuler.Y, newEuler.Z);
        _lastDragAngle = currentAngle;

        return true;
    }
}


