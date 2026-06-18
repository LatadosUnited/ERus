using System.Collections.Generic;
using System.Linq;
using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Managers;

public class SelectionManager
{
    public HashSet<Entity> SelectedEntities { get; } = new();

    public Entity? SelectedEntity
    {
        get => SelectedEntities.Count > 0 ? SelectedEntities.First() : null;
        set
        {
            SelectedEntities.Clear();
            if (value.HasValue)
                SelectedEntities.Add(value.Value);
        }
    }

    public void ToggleSelection(Entity entity)
    {
        if (!SelectedEntities.Remove(entity))
            SelectedEntities.Add(entity);
    }

    public void Select(Entity entity, bool additive)
    {
        if (additive)
        {
            SelectedEntities.Add(entity);
        }
        else
        {
            SelectedEntities.Clear();
            SelectedEntities.Add(entity);
        }
    }

    public void ClearSelection()
    {
        SelectedEntities.Clear();
    }
}
