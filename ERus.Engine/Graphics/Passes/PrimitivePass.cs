using System;
using System.Numerics;
using Silk.NET.OpenGL;
using ERus.Engine.ECS;
using ERus.Engine.Graphics.Buffers;
using ERus.Engine.Graphics.Shaders;

namespace ERus.Engine.Graphics.Passes;

/// <summary>
/// Desenha as malhas primitivas (cubo, esfera, plano, cápsula, cilindro, quad)
/// com material ou sprite aplicado.
/// </summary>
public sealed class PrimitivePass : IDisposable
{
    private readonly ShaderProgram _shader;
    private readonly PrimitiveMeshBuffers _meshes;

    public PrimitivePass(GL gl, PrimitiveMeshBuffers meshes)
    {
        _shader = new ShaderProgram(gl, "Primitive", ShaderSources.PrimitiveVertex, ShaderSources.PrimitiveFragment);
        _meshes = meshes;
    }

    public void Draw(PrimitiveMeshType type, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, SurfaceParams surface)
    {
        _shader.Use();
        _shader.SetMatrix4("uView", view);
        _shader.SetMatrix4("uProjection", projection);
        _shader.SetMatrix4("uModel", model);

        _shader.SetVector4("uColorTint", surface.Tint);
        _shader.SetVector2("uTiling", surface.Tiling);
        _shader.SetVector2("uOffset", surface.Offset);
        _shader.SetFloat("uAlphaCutoff", surface.AlphaCutoff);

        BindAlbedo(surface.TextureGuid);

        _meshes.Draw(type);
    }

    /// <summary>
    /// Liga a textura de albedo no slot 0. Sem textura (ou com GUID que não resolve)
    /// usa a textura branca e desliga a amostragem no shader.
    /// </summary>
    private void BindAlbedo(Guid textureGuid)
    {
        var assets = ERus.Engine.Assets.AssetManager.Get();

        var texture = textureGuid != Guid.Empty ? assets.LoadTextureByGuid(textureGuid) : null;
        if (texture != null)
        {
            texture.Bind(TextureUnit.Texture0);
            _shader.SetInt("uHasTexture", 1);
            return;
        }

        assets.WhiteTexture.Bind(TextureUnit.Texture0);
        _shader.SetInt("uHasTexture", 0);
    }

    public void Dispose() => _shader.Dispose();
}
