using System;
using Silk.NET.OpenGL;

namespace ERus.Engine.Assets;

public enum TextureType
{
    Diffuse,
    Specular,
    Normal,
    Height
}

public class Texture : IDisposable
{
    public uint Id { get; private set; }
    public string Type { get; private set; }
    public string Path { get; private set; }
    
    private GL _gl;

    public Texture(GL gl, uint id, string type, string path)
    {
        _gl = gl;
        Id = id;
        Type = type;
        Path = path;
    }

    public void Bind(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(textureSlot);
        _gl.BindTexture(TextureTarget.Texture2D, Id);
    }

    public void Dispose()
    {
        if (Id != 0)
        {
            _gl.DeleteTexture(Id);
            Id = 0;
        }
    }
}
