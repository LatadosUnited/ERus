using System;
using System.Collections.Generic;
using System.Linq;
using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Managers;

public class SelectionManager
{
    public HashSet<Entity> SelectedEntities { get; } = new();

    public event Action<Entity?>? OnSelectionChanged;

    public Entity? SelectedEntity
    {
        get => SelectedEntities.Count > 0 ? SelectedEntities.First() : null;
        set
        {
            var prev = SelectedEntity;
            SelectedEntities.Clear();
            if (value.HasValue)
                SelectedEntities.Add(value.Value);

            if (prev != value)
                OnSelectionChanged?.Invoke(value);
        }
    }

    public void ToggleSelection(Entity entity)
    {
        if (!SelectedEntities.Remove(entity))
            SelectedEntities.Add(entity);

        OnSelectionChanged?.Invoke(SelectedEntity);
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
        OnSelectionChanged?.Invoke(SelectedEntity);
    }

    public void ClearSelection()
    {
        bool hadSelection = SelectedEntities.Count > 0;
        SelectedEntities.Clear();
        if (hadSelection)
            OnSelectionChanged?.Invoke(null);
    }
}
