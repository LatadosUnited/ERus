using System.Collections.Generic;

namespace ERus.Engine.Graphics;

/// <summary>
/// Responsável por carregar, cachear e despachar assets físicos do disco (Texturas, Modelos 3D, Shaders).
/// </summary>
public class AssetManager
{
    private readonly Dictionary<string, uint> _textureCache = new Dictionary<string, uint>();

    public uint GetOrLoadTexture(string filepath)
    {
        if (_textureCache.TryGetValue(filepath, out uint texId))
        {
            return texId; // Retorna do Cache
        }

        // TODO: Na próxima etapa usaremos StbImageSharp para:
        // 1. Abrir a imagem do disco
        // 2. Extrair os pixels em formato RGBA
        // 3. Gerar um _gl.GenTexture() e preencher com _gl.TexImage2D()
        
        return 0; // ID inválido (temporário)
    }

    public void ClearCache()
    {
        _textureCache.Clear();
        // TODO: Chamar _gl.DeleteTexture() em todos os valores
    }
}
