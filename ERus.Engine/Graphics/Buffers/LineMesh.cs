using System;
using Silk.NET.OpenGL;

namespace ERus.Engine.Graphics.Buffers;

/// <summary>
/// Malha estática de linhas com cor por vértice.
/// Layout do vértice: 6 floats (posição 3, cor 3).
/// </summary>
public sealed class LineMesh : IDisposable
{
    private const int VertexStride = 6;

    private readonly GL _gl;
    private readonly uint _vao;
    private readonly uint _vbo;

    public int VertexCount { get; }

    public LineMesh(GL gl, float[] vertices)
    {
        _gl = gl;
        VertexCount = vertices.Length / VertexStride;

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        unsafe
        {
            fixed (float* buffer = vertices)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), buffer, BufferUsageARB.StaticDraw);
            }

            int stride = VertexStride * sizeof(float);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)stride, (void*)0);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, (uint)stride, (void*)(3 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    public void Bind() => _gl.BindVertexArray(_vao);

    public void Draw() => _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)VertexCount);

    public void BindAndDraw()
    {
        Bind();
        Draw();
    }

    public void Dispose()
    {
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
    }
}
