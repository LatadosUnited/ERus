using System.Collections.Generic;
using System.Numerics;

namespace ERus.Engine.Assets;

public class BoneInfo
{
    // O ID final que será enviado ao shader (índice no array uFinalBonesMatrices)
    public int Id { get; set; }
    
    // Matriz de compensação (transforma os vértices do espaço local da malha para o espaço local do osso)
    public Matrix4x4 Offset { get; set; }
}

public struct KeyPosition
{
    public Vector3 Position;
    public float TimeStamp;
}

public struct KeyRotation
{
    public Quaternion Orientation;
    public float TimeStamp;
}

public struct KeyScale
{
    public Vector3 Scale;
    public float TimeStamp;
}

public class BoneAnimationChannel
{
    public string NodeName { get; set; } = string.Empty;
    public List<KeyPosition> Positions { get; set; } = new();
    public List<KeyRotation> Rotations { get; set; } = new();
    public List<KeyScale> Scales { get; set; } = new();

    public int GetPositionIndex(float animationTime)
    {
        for (int index = 0; index < Positions.Count - 1; ++index)
        {
            if (animationTime < Positions[index + 1].TimeStamp)
                return index;
        }
        return 0;
    }

    public int GetRotationIndex(float animationTime)
    {
        for (int index = 0; index < Rotations.Count - 1; ++index)
        {
            if (animationTime < Rotations[index + 1].TimeStamp)
                return index;
        }
        return 0;
    }

    public int GetScaleIndex(float animationTime)
    {
        for (int index = 0; index < Scales.Count - 1; ++index)
        {
            if (animationTime < Scales[index + 1].TimeStamp)
                return index;
        }
        return 0;
    }
}

public class NodeHierarchy
{
    public string Name { get; set; } = string.Empty;
    public Matrix4x4 Transformation { get; set; } = Matrix4x4.Identity;
    public List<NodeHierarchy> Children { get; set; } = new();
}

public class AnimationData
{
    public string Name { get; set; } = string.Empty;
    public float Duration { get; set; }
    public float TicksPerSecond { get; set; }
    public Dictionary<string, BoneAnimationChannel> Channels { get; set; } = new();
    public NodeHierarchy RootNode { get; set; } = new();
    public Dictionary<string, BoneInfo> BoneInfoMap { get; set; } = new();
}
