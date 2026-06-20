using System;
using System.Collections.Generic;

namespace ERus.Engine.Assets;

public class Model : IDisposable
{
    public string Path { get; private set; }
    public List<Mesh> Meshes { get; private set; }
    
    public Dictionary<string, BoneInfo> BoneInfoMap { get; private set; }
    public int BoneCounter { get; set; } = 0;
    
    public Dictionary<string, AnimationData> Animations { get; private set; }
    public NodeHierarchy RootNode { get; set; } = new();

    public Model(string path)
    {
        Path = path;
        Meshes = new List<Mesh>();
        BoneInfoMap = new Dictionary<string, BoneInfo>();
        Animations = new Dictionary<string, AnimationData>();
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
