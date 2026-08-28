using System;
using System.Numerics;
using ERus.Engine.ECS;

namespace ERus.Engine.Graphics.Passes;

/// <summary>
/// Parâmetros visuais de uma entidade, normalizados a partir de
/// <see cref="MaterialComponent"/> ou <see cref="SpriteRendererComponent"/>.
/// Os dois alimentam o mesmo shader; o sprite expressa o flip como tiling negativo.
/// </summary>
public readonly struct SurfaceParams
{
    public Vector4 Tint { get; init; }
    public Vector2 Tiling { get; init; }
    public Vector2 Offset { get; init; }
    public float AlphaCutoff { get; init; }
    public Guid TextureGuid { get; init; }

    /// <summary>Cutoff mínimo do sprite, para que o fundo transparente não escreva no depth.</summary>
    private const float SpriteAlphaCutoff = 0.01f;

    public static SurfaceParams Default => new()
    {
        Tint = Vector4.One,
        Tiling = Vector2.One,
        Offset = Vector2.Zero,
        AlphaCutoff = 0.0f,
        TextureGuid = Guid.Empty
    };

    /// <summary>
    /// Material tem precedência sobre sprite quando a entidade tem os dois.
    /// Sem nenhum dos dois, devolve o padrão neutro (branco, sem textura).
    /// </summary>
    public static SurfaceParams From(Registry registry, Entity entity)
    {
        if (registry.HasComponentByType(entity, typeof(MaterialComponent)))
        {
            ref var mat = ref registry.GetComponent<MaterialComponent>(entity);
            return new SurfaceParams
            {
                Tint = mat.ColorTint,
                Tiling = mat.Tiling,
                Offset = mat.Offset,
                AlphaCutoff = mat.AlphaCutoff,
                TextureGuid = mat.AlbedoTextureGuid
            };
        }

        if (registry.HasComponentByType(entity, typeof(SpriteRendererComponent)))
        {
            ref var sprite = ref registry.GetComponent<SpriteRendererComponent>(entity);
            return new SurfaceParams
            {
                Tint = sprite.Color,
                // Espelhar a UV inverte o sprite sem precisar de outra malha.
                Tiling = new Vector2(sprite.FlipX ? -1f : 1f, sprite.FlipY ? -1f : 1f),
                Offset = new Vector2(sprite.FlipX ? 1f : 0f, sprite.FlipY ? 1f : 0f),
                AlphaCutoff = SpriteAlphaCutoff,
                TextureGuid = sprite.SpriteGuid
            };
        }

        return Default;
    }
}
