using System;
using System.Numerics;
using Silk.NET.OpenGL;
using ERus.Engine.ECS;
using ERus.Engine.Graphics.Buffers;
using ERus.Engine.Graphics.Passes;

namespace ERus.Engine.Graphics;

/// <summary>
/// Percorre a cena e despacha cada entidade para o pass adequado.
/// Não compila shaders nem cria buffers: isso pertence a <see cref="PrimitivePass"/>,
/// <see cref="ModelPass"/> e <see cref="LinePass"/>.
/// </summary>
public class SceneRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly PrimitiveMeshBuffers _primitives;
    private readonly PrimitivePass _primitivePass;
    private readonly ModelPass _modelPass;
    private readonly LinePass _linePass;

    /// <summary>Contadores do último frame, para diagnóstico e profiler.</summary>
    public RenderStats Stats { get; private set; }

    public SceneRenderer(GL gl)
    {
        _gl = gl;
        _primitives = new PrimitiveMeshBuffers(gl);
        _primitivePass = new PrimitivePass(gl, _primitives);
        _modelPass = new ModelPass(gl);
        _linePass = new LinePass(gl);
    }

    public void Draw(Registry registry, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix,
        Entity? selectedEntity = null, bool isLocked = false, bool drawGrid = true)
    {
        BeginFrame();

        if (drawGrid) _linePass.DrawGrid(viewMatrix, projectionMatrix);

        DrawEntities(registry, viewMatrix, projectionMatrix);

        if (drawGrid) _linePass.DrawCameraGizmos(registry, viewMatrix, projectionMatrix);

        EndFrame();
    }

    private void BeginFrame()
    {
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    private void EndFrame()
    {
        _gl.BindVertexArray(0);
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.DepthTest);
    }

    private void DrawEntities(Registry registry, Matrix4x4 view, Matrix4x4 projection)
    {
        var frustum = new ERus.Engine.Math.Frustum(view * projection);
        var stats = new RenderStats();

        foreach (var entity in registry.View<TransformComponent, MeshComponent>())
        {
            ref var transform = ref registry.GetComponent<TransformComponent>(entity);
            ref var mesh = ref registry.GetComponent<MeshComponent>(entity);

            // Um AssetGuid definido manda na renderização mesmo que o caminho não
            // resolva: nesse caso nada é desenhado, em vez de cair na primitiva —
            // o Type costuma carregar um placeholder que não representa a entidade.
            bool usesImportedModel = mesh.AssetGuid != Guid.Empty;
            string? modelPath = usesImportedModel ? ResolveModelPath(mesh) : null;

            if (IsCulled(frustum, transform, mesh, usesImportedModel, modelPath))
            {
                stats.EntitiesCulled++;
                continue;
            }
            stats.EntitiesDrawn++;

            var model = TransformMath.ModelMatrix(transform.Position, transform.Rotation, transform.Scale);
            var surface = SurfaceParams.From(registry, entity);

            if (usesImportedModel)
            {
                if (modelPath != null)
                    _modelPass.Draw(registry, entity, modelPath, model, view, projection, surface);
            }
            else if (PrimitiveMeshBuffers.IsValid(mesh.Type))
            {
                _primitivePass.Draw(mesh.Type, model, view, projection, surface);
            }
        }

        Stats = stats;
    }

    /// <summary>Caminho do modelo importado, ou null quando a entidade usa uma primitiva.</summary>
    private static string? ResolveModelPath(in MeshComponent mesh)
    {
        if (mesh.AssetGuid == Guid.Empty) return null;

        string? path = Core.Engine.Instance?.AssetDatabase.GetPathByGuid(mesh.AssetGuid);
        return string.IsNullOrEmpty(path) ? null : path;
    }

    /// <summary>
    /// Testa a esfera envolvente da entidade contra o frustum da câmera.
    /// O raio base vem do modelo carregado ou da primitiva, escalado pelo maior eixo
    /// (aproximação conservadora: nunca descarta algo que estaria visível).
    /// </summary>
    private bool IsCulled(in ERus.Engine.Math.Frustum frustum, in TransformComponent transform, in MeshComponent mesh,
        bool usesImportedModel, string? modelPath)
    {
        float baseRadius = 1.0f;

        if (usesImportedModel)
        {
            var model = modelPath != null ? ERus.Engine.Assets.AssetManager.Get().LoadModel(modelPath) : null;
            if (model != null) baseRadius = model.BoundingRadius;
        }
        else if (PrimitiveMeshBuffers.IsValid(mesh.Type))
        {
            baseRadius = _primitives.BoundingRadius(mesh.Type);
        }

        float maxScale = MathF.Max(transform.Scale.X, MathF.Max(transform.Scale.Y, transform.Scale.Z));
        var position = new Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);

        return !frustum.IntersectsSphere(position, baseRadius * maxScale);
    }

    public void Dispose()
    {
        _primitivePass.Dispose();
        _modelPass.Dispose();
        _linePass.Dispose();
        _primitives.Dispose();
    }
}
