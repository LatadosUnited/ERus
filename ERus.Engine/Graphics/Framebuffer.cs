using Silk.NET.OpenGL;
using System;

namespace ERus.Engine.Graphics;

public class GLFramebuffer : IDisposable
{
    private readonly GL _gl;
    public uint FboId { get; private set; }
    public uint TextureId { get; private set; }
    public uint RboId { get; private set; }

    public int Width { get; private set; }
    public int Height { get; private set; }

    public GLFramebuffer(GL gl, int width, int height)
    {
        _gl = gl;
        Invalidate(width, height);
    }

    public void Invalidate(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        Width = width;
        Height = height;

        Dispose(); // Limpa recursos antigos se existirem

        // Cria o Framebuffer
        FboId = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, FboId);

        // Cria a Textura de Cor
        TextureId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, TextureId);
        
        unsafe 
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, TextureId, 0);

        // Cria o Renderbuffer para Profundidade (Depth)
        RboId = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, RboId);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)width, (uint)height);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, RboId);

        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            Console.WriteLine($"[OpenGL] Erro ao criar Framebuffer: {status}");
        }

        // Desfaz os Binds
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
    }

    public void Bind()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, FboId);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
    }

    public void Unbind(Silk.NET.Maths.Vector2D<int> mainFramebufferSize)
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        // Restaura o viewport para o tamanho real do framebuffer da janela principal
        _gl.Viewport(0, 0, (uint)mainFramebufferSize.X, (uint)mainFramebufferSize.Y);
    }

    public void Dispose()
    {
        if (FboId != 0)
        {
            _gl.DeleteFramebuffer(FboId);
            _gl.DeleteTexture(TextureId);
            _gl.DeleteRenderbuffer(RboId);
            FboId = 0;
            TextureId = 0;
            RboId = 0;
        }
    }
}
