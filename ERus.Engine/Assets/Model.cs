using System;
using System.Collections.Generic;

namespace ERus.Engine.Assets;

public class Model : IDisposable
{
    public string Path { get; private set; }
    public List<Mesh> Meshes { get; private set; }

    public Model(string path)
    {
        Path = path;
        Meshes = new List<Mesh>();
    }

    public void AddMesh(Mesh mesh)
    {
        Meshes.Add(mesh);
    }

    public void Draw(uint shaderProgram)
    {
        foreach (var mesh in Meshes)
        {
            mesh.Draw(shaderProgram);
        }
    }

    public void Dispose()
    {
        foreach (var mesh in Meshes)
        {
            mesh.Dispose();
        }
        Meshes.Clear();
    }
}
