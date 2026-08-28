using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenGL;
using ERus.Engine.Scripting;

namespace ERus.Engine.Graphics.Shaders;

/// <summary>
/// Programa de shader compilado e linkado, com cache de uniform locations.
/// Substitui o padrão de manter um <c>uint</c> de programa e um campo <c>int</c>
/// por uniform espalhados pelo renderer.
/// </summary>
public sealed class ShaderProgram : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, int> _uniformLocations = new();
    private readonly string _name;

    public uint Handle { get; }

    public ShaderProgram(GL gl, string name, string vertexSource, string fragmentSource)
    {
        _gl = gl;
        _name = name;

        uint vertex = Compile(ShaderType.VertexShader, vertexSource);
        uint fragment = Compile(ShaderType.FragmentShader, fragmentSource);

        Handle = _gl.CreateProgram();
        _gl.AttachShader(Handle, vertex);
        _gl.AttachShader(Handle, fragment);
        _gl.LinkProgram(Handle);

        _gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
            ConsoleLog.Error($"[Shader] Falha ao linkar '{_name}': {_gl.GetProgramInfoLog(Handle)}");

        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);
    }

    private uint Compile(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        // O código anterior ignorava falhas de compilação em silêncio — um shader
        // quebrado virava tela preta sem nenhuma pista no console.
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compiled);
        if (compiled == 0)
            ConsoleLog.Error($"[Shader] Falha ao compilar {type} de '{_name}': {_gl.GetShaderInfoLog(shader)}");

        return shader;
    }

    public void Use() => _gl.UseProgram(Handle);

    /// <summary>Location do uniform, resolvida uma única vez por nome. -1 se não existir.</summary>
    public int Location(string name)
    {
        if (_uniformLocations.TryGetValue(name, out int cached)) return cached;

        int location = _gl.GetUniformLocation(Handle, name);
        _uniformLocations[name] = location;
        return location;
    }

    // --- Setters (exigem que o programa esteja em uso) ----------------------

    public unsafe void SetMatrix4(string name, Matrix4x4 value)
    {
        int location = Location(name);
        if (location == -1) return;
        _gl.UniformMatrix4(location, 1, false, (float*)&value);
    }

    /// <summary>Variante por location, para uniforms de array resolvidos em lote.</summary>
    public unsafe void SetMatrix4(int location, Matrix4x4 value)
    {
        if (location == -1) return;
        _gl.UniformMatrix4(location, 1, false, (float*)&value);
    }

    public void SetVector4(string name, Vector4 value)
    {
        int location = Location(name);
        if (location == -1) return;
        _gl.Uniform4(location, value.X, value.Y, value.Z, value.W);
    }

    public void SetVector3(string name, Vector3 value)
    {
        int location = Location(name);
        if (location == -1) return;
        _gl.Uniform3(location, value.X, value.Y, value.Z);
    }

    public void SetVector2(string name, Vector2 value)
    {
        int location = Location(name);
        if (location == -1) return;
        _gl.Uniform2(location, value.X, value.Y);
    }

    public void SetInt(string name, int value)
    {
        int location = Location(name);
        if (location == -1) return;
        _gl.Uniform1(location, value);
    }

    public void SetFloat(string name, float value)
    {
        int location = Location(name);
        if (location == -1) return;
        _gl.Uniform1(location, value);
    }

    public void Dispose()
    {
        if (Handle != 0) _gl.DeleteProgram(Handle);
    }
}
