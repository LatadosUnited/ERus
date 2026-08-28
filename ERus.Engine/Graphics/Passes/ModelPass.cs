using System;
using System.Numerics;
using Silk.NET.OpenGL;
using ERus.Engine.ECS;
using ERus.Engine.Graphics.Shaders;

namespace ERus.Engine.Graphics.Passes;

/// <summary>
/// Desenha modelos importados (Assimp), aplicando skinning quando a entidade tem
/// <see cref="AnimatorComponent"/>.
/// </summary>
public sealed class ModelPass : IDisposable
{
    /// <summary>Precisa bater com MAX_BONES no shader de modelo.</summary>
    public const int MaxBones = 100;

    private readonly GL _gl;
    private readonly ShaderProgram _shader;

    /// <summary>Locations de uFinalBonesMatrices[i], resolvidas uma vez na criação.</summary>
    private readonly int[] _boneMatrixLocations = new int[MaxBones];

    public ModelPass(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, "Model", ShaderSources.ModelVertex, ShaderSources.ModelFragment);

        for (int i = 0; i < MaxBones; i++)
            _boneMatrixLocations[i] = _shader.Location($"uFinalBonesMatrices[{i}]");
    }

    public void Draw(Registry registry, Entity entity, string modelPath, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, SurfaceParams surface)
    {
        _shader.Use();
        _shader.SetVector3("uColorTint", new Vector3(surface.Tint.X, surface.Tint.Y, surface.Tint.Z));
        _shader.SetMatrix4("uView", view);
        _shader.SetMatrix4("uProjection", projection);
        _shader.SetMatrix4("uModel", model);

        UploadBoneMatrices(registry, entity);

        var loaded = ERus.Engine.Assets.AssetManager.Get().LoadModel(modelPath);
        loaded?.Draw(_shader.Handle);

        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Sem animator, envia identidade: o vértice cai no caminho "sem bones" do shader
    /// e o modelo é desenhado na pose de bind.
    /// </summary>
    private void UploadBoneMatrices(Registry registry, Entity entity)
    {
        bool animated = registry.HasComponentByType(entity, typeof(AnimatorComponent));

        if (!animated)
        {
            for (int i = 0; i < MaxBones; i++)
                _shader.SetMatrix4(_boneMatrixLocations[i], Matrix4x4.Identity);
            return;
        }

        ref var animator = ref registry.GetComponent<AnimatorComponent>(entity);
        for (int i = 0; i < MaxBones; i++)
            _shader.SetMatrix4(_boneMatrixLocations[i], animator.FinalBoneMatrices[i]);
    }

    public void Dispose() => _shader.Dispose();
}
