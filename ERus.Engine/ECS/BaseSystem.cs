namespace ERus.Engine.ECS;

public abstract class BaseSystem
{
    protected Registry Registry { get; }

    protected BaseSystem(Registry registry)
    {
        Registry = registry;
    }

    public abstract void Update(double deltaTime);
}
