using System;
using Silk.NET.OpenGL;
using ERus.Engine.ECS;

namespace ERus.Engine.Graphics.Buffers;

/// <summary>
/// Buffers de GPU das malhas primitivas, indexados por <see cref="PrimitiveMeshType"/>.
/// Layout do vértice: 8 floats (posição 3, normal 3, UV 2).
/// </summary>
public sealed class PrimitiveMeshBuffers : IDisposable
{
    /// <summary>Índice 0 é <see cref="PrimitiveMeshType.None"/>; os slots válidos vão de 1 a 6.</summary>
    private const int SlotCount = 7;
    private const int FirstSlot = 1;
    private const int LastSlot = 6;
    private const int VertexStride = 8;

    private readonly GL _gl;
    private readonly uint[] _vao = new uint[SlotCount];
    private readonly uint[] _vbo = new uint[SlotCount];
    private readonly uint[] _ebo = new uint[SlotCount];
    private readonly int[] _indexCount = new int[SlotCount];
    private readonly float[] _boundingRadius = new float[SlotCount];

    public PrimitiveMeshBuffers(GL gl)
    {
        _gl = gl;

        var meshes = new MeshData?[SlotCount];
        meshes[(int)PrimitiveMeshType.Cube] = PrimitiveMeshGenerator.GenerateCube();
        meshes[(int)PrimitiveMeshType.Sphere] = PrimitiveMeshGenerator.GenerateSphere();
        meshes[(int)PrimitiveMeshType.Plane] = PrimitiveMeshGenerator.GeneratePlane();
        meshes[(int)PrimitiveMeshType.Capsule] = PrimitiveMeshGenerator.GenerateCapsule();
        meshes[(int)PrimitiveMeshType.Cylinder] = PrimitiveMeshGenerator.GenerateCylinder();
        meshes[(int)PrimitiveMeshType.Quad] = PrimitiveMeshGenerator.GenerateQuad();

        for (int i = FirstSlot; i <= LastSlot; i++)
        {
            if (meshes[i] == null) continue;
            Upload(i, meshes[i]!);
        }
    }

    /// <summary>Raio da esfera envolvente da primitiva, usado no frustum culling.</summary>
    public float BoundingRadius(PrimitiveMeshType type)
        => IsValid(type) ? _boundingRadius[(int)type] : 1.0f;

    public static bool IsValid(PrimitiveMeshType type)
        => type != PrimitiveMeshType.None && (int)type >= FirstSlot && (int)type <= LastSlot;

    /// <summary>Desenha a primitiva. Retorna false se o slot não tiver buffer criado.</summary>
    public unsafe bool Draw(PrimitiveMeshType type)
    {
        if (!IsValid(type)) return false;

        int slot = (int)type;
        if (_vao[slot] == 0) return false;

        _gl.BindVertexArray(_vao[slot]);
        _gl.DrawElements(PrimitiveType.Triangles, (uint)_indexCount[slot], DrawElementsType.UnsignedInt, (void*)0);
        return true;
    }

    private unsafe void Upload(int slot, MeshData data)
    {
        _vao[slot] = _gl.GenVertexArray();
        _vbo[slot] = _gl.GenBuffer();
        _ebo[slot] = _gl.GenBuffer();
        _indexCount[slot] = data.Indices.Length;
        _boundingRadius[slot] = data.BoundingRadius;

        _gl.BindVertexArray(_vao[slot]);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo[slot]);
        fixed (float* vertices = data.Vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Vertices.Length * sizeof(float)), vertices, BufferUsageARB.StaticDraw);
        }

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo[slot]);
        fixed (uint* indices = data.Indices)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(data.Indices.Length * sizeof(uint)), indices, BufferUsageARB.StaticDraw);
        }

        int stride = VertexStride * sizeof(float);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)stride, (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, (uint)stride, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, (uint)stride, (void*)(6 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);

        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        for (int i = FirstSlot; i <= LastSlot; i++)
        {
            if (_vao[i] != 0) _gl.DeleteVertexArray(_vao[i]);
            if (_vbo[i] != 0) _gl.DeleteBuffer(_vbo[i]);
            if (_ebo[i] != 0) _gl.DeleteBuffer(_ebo[i]);
        }
    }
}
