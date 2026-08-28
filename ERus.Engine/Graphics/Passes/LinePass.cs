using System;
using System.Numerics;
using Silk.NET.OpenGL;
using ERus.Engine.ECS;
using ERus.Engine.Graphics.Buffers;
using ERus.Engine.Graphics.Gizmos;
using ERus.Engine.Graphics.Shaders;

namespace ERus.Engine.Graphics.Passes;

/// <summary>
/// Desenha a geometria auxiliar do editor em linhas: a grade do chão e um ícone
/// de wireframe em cada entidade com câmera.
/// </summary>
public sealed class LinePass : IDisposable
{
    private readonly ShaderProgram _shader;
    private readonly LineMesh _grid;
    private readonly LineMesh _cameraGizmo;

    public LinePass(GL gl)
    {
        _shader = new ShaderProgram(gl, "Line", ShaderSources.LineVertex, ShaderSources.LineFragment);
        _grid = EditorGizmoMeshes.BuildGrid(gl);
        _cameraGizmo = EditorGizmoMeshes.BuildCameraGizmo(gl);
    }

    public void DrawGrid(Matrix4x4 view, Matrix4x4 projection)
    {
        BeginLines(view, projection);
        _shader.SetMatrix4("uModel", Matrix4x4.Identity);
        _grid.BindAndDraw();
    }

    public void DrawCameraGizmos(Registry registry, Matrix4x4 view, Matrix4x4 projection)
    {
        BeginLines(view, projection);
        _cameraGizmo.Bind();

        foreach (var entity in registry.View<TransformComponent, CameraComponent>())
        {
            ref var transform = ref registry.GetComponent<TransformComponent>(entity);

            // O ícone não acompanha a escala da entidade: mantém tamanho constante na cena.
            var model = TransformMath.RotationMatrix(transform.Rotation)
                      * Matrix4x4.CreateTranslation(transform.Position.X, transform.Position.Y, transform.Position.Z);

            _shader.SetMatrix4("uModel", model);
            _cameraGizmo.Draw();
        }
    }

    private void BeginLines(Matrix4x4 view, Matrix4x4 projection)
    {
        _shader.Use();
        _shader.SetMatrix4("uView", view);
        _shader.SetMatrix4("uProjection", projection);
    }

    public void Dispose()
    {
        _shader.Dispose();
        _grid.Dispose();
        _cameraGizmo.Dispose();
    }
}
