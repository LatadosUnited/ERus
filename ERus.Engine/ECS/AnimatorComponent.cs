using System.Numerics;

namespace ERus.Engine.ECS;

public struct AnimatorComponent : IComponent
{
    // Nome da animação atual a ser tocada (deve existir em Model.Animations)
    public string CurrentAnimationName { get; set; }
    
    // Tempo atual da animação em segundos ou ticks
    public float CurrentTime { get; set; }
    
    // Velocidade de reprodução (1.0 = normal)
    public float PlaybackSpeed { get; set; }
    
    // Flag de play/pause
    public bool IsPlaying { get; set; }

    // Array com as matrizes finais calculadas pelo AnimatorSystem, limitadas a 100 para o Shader
    // Referência em classe para evitar cópia excessiva (structs devem ser leves)
    [NonSerializedComponent]
    public Matrix4x4[] FinalBoneMatrices { get; set; }

    public AnimatorComponent()
    {
        CurrentAnimationName = string.Empty;
        CurrentTime = 0.0f;
        PlaybackSpeed = 1.0f;
        IsPlaying = true;
        FinalBoneMatrices = new Matrix4x4[100];
        
        for (int i = 0; i < 100; i++)
        {
            FinalBoneMatrices[i] = Matrix4x4.Identity;
        }
    }
}
