using System.Numerics;
using System.Runtime.InteropServices;

namespace ERus.Engine.Assets;

[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TexCoords;
    public Vector3 Tangent;
    public Vector3 Bitangent;
    
    // Suporte para Animação Esqueletal
    public const int MaxBoneInfluence = 4;
    
    // Arrays fixos para os IDs dos ossos e seus respectivos pesos
    public unsafe fixed int BoneIDs[MaxBoneInfluence];
    public unsafe fixed float Weights[MaxBoneInfluence];
}
