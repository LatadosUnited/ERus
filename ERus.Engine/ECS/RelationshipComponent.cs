namespace ERus.Engine.ECS;

public struct RelationshipComponent : IComponent
{
    public Entity? Parent = null;
    public Entity? FirstChild = null;
    public Entity? PrevSibling = null;
    public Entity? NextSibling = null;

    public int ChildrenCount = 0;

    public RelationshipComponent() {}
}
