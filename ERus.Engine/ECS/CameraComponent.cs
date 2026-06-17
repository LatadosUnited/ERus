using System;

namespace ERus.Engine.ECS;

public struct CameraComponent : IComponent
{
    public float FieldOfView;
    public bool IsPrimary;
    public float NearClip;
    public float FarClip;

    // Default constructor equivalency
    public CameraComponent()
    {
        FieldOfView = 45.0f;
        IsPrimary = true;
        NearClip = 0.1f;
        FarClip = 1000.0f;
    }
}
