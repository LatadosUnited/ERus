using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;

namespace ERus.Engine.Assets;

public class Mesh : IDisposable
{
    public List<Vertex> Vertices { get; private set; }
    public List<uint> Indices { get; private set; }
    public List<Texture> Textures { get; private set; }

    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private GL _gl;

    public Mesh(GL gl, List<Vertex> vertices, List<uint> indices, List<Texture> textures)
    {
        _gl = gl;
        Vertices = vertices;
        Indices = indices;
        Textures = textures;

        SetupMesh();
    }

    private unsafe void SetupMesh()
    {
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (Vertex* buf = Vertices.ToArray())
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(Vertices.Count * sizeof(Vertex)), buf, BufferUsageARB.StaticDraw);
        }

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* buf = Indices.ToArray())
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(Indices.Count * sizeof(uint)), buf, BufferUsageARB.StaticDraw);
        }

        // Posição: vec3 (0 a 11 bytes)
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)0);

        // Normal: vec3 (12 a 23 bytes)
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)(3 * sizeof(float)));

        // TexCoords: vec2 (24 a 31 bytes)
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)(6 * sizeof(float)));

        // Tangent: vec3
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)(8 * sizeof(float)));

        // Bitangent: vec3
        _gl.EnableVertexAttribArray(4);
        _gl.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)(11 * sizeof(float)));

        _gl.BindVertexArray(0);
    }

    public unsafe void Draw(uint shaderProgram)
    {
        uint diffuseNr = 1;
        uint specularNr = 1;
        uint normalNr = 1;
        uint heightNr = 1;

        for (int i = 0; i < Textures.Count; i++)
        {
            _gl.ActiveTexture(TextureUnit.Texture0 + i);

            string number = "";
            string name = Textures[i].Type;

            if (name == "texture_diffuse") number = (diffuseNr++).ToString();
            else if (name == "texture_specular") number = (specularNr++).ToString();
            else if (name == "texture_normal") number = (normalNr++).ToString();
            else if (name == "texture_height") number = (heightNr++).ToString();

            int loc = _gl.GetUniformLocation(shaderProgram, name + number);
            if (loc >= 0)
            {
                _gl.Uniform1(loc, i);
            }
            
            _gl.BindTexture(TextureTarget.Texture2D, Textures[i].Id);
        }

        _gl.BindVertexArray(_vao);
        _gl.DrawElements(PrimitiveType.Triangles, (uint)Indices.Count, DrawElementsType.UnsignedInt, (void*)0);
        _gl.BindVertexArray(0);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    public void Dispose()
    {
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_ebo != 0) _gl.DeleteBuffer(_ebo);
    }
}
