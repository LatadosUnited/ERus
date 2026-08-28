using System.Collections.Generic;
using System.Numerics;

namespace ERus.Engine.Graphics.Buffers;

/// <summary>
/// Acumula segmentos de linha coloridos e produz o array de vértices
/// no layout esperado por <see cref="LineMesh"/>.
/// </summary>
public sealed class LineMeshBuilder
{
    private readonly List<float> _vertices = new();

    public void AddLine(Vector3 from, Vector3 to, Vector3 color)
    {
        AddVertex(from, color);
        AddVertex(to, color);
    }

    private void AddVertex(Vector3 position, Vector3 color)
    {
        _vertices.Add(position.X);
        _vertices.Add(position.Y);
        _vertices.Add(position.Z);
        _vertices.Add(color.X);
        _vertices.Add(color.Y);
        _vertices.Add(color.Z);
    }

    public float[] ToArray() => _vertices.ToArray();
}
