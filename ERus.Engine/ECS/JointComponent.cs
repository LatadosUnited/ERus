namespace ERus.Engine.ECS;

public enum JointType
{
    Fixed,
    Hinge,
    Spring
}

public struct JointComponent : IComponent
{
    public JointType Type { get; set; }
    
    // O ID da entidade alvo para se amarrar.
    public int TargetEntityId { get; set; }
    
    // Referência interna para amarra na biblioteca de física (Jitter2.Dynamics.Constraints.Constraint)
    public object? InternalConstraint { get; set; }

    public JointComponent()
    {
        Type = JointType.Fixed;
        TargetEntityId = -1;
        InternalConstraint = null;
    }
}
