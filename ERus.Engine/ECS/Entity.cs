namespace ERus.Engine.ECS;

public readonly struct Entity
{
    public readonly int Id;
    public Entity(int id) => Id = id;

    public override bool Equals(object? obj) => obj is Entity e && e.Id == Id;
    public override int GetHashCode() => Id.GetHashCode();
    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    public static bool operator !=(Entity left, Entity right) => !(left == right);
}
