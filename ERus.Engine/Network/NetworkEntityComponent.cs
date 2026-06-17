using ERus.Engine.ECS;

namespace ERus.Engine.Network;

public struct NetworkEntityComponent : IComponent
{
    public int NetworkId;
    public bool IsLocked;
    public int LockedByUserId; // ID do usuário que trancou a edição
}
