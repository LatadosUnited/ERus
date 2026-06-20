using System;

namespace ERus.Engine.ECS;

public static class RelationshipSystem
{
    public static void SetParent(Entity child, Entity? newParent, Registry registry)
    {
        if (newParent.HasValue)
        {
            Entity? cur = newParent;
            while (cur != null)
            {
                if (cur.Value.Id == child.Id) return; 
                if (registry.HasComponent<RelationshipComponent>(cur.Value))
                    cur = registry.GetComponent<RelationshipComponent>(cur.Value).Parent;
                else
                    cur = null;
            }
        }

        if (!registry.HasComponent<RelationshipComponent>(child))
            registry.AddComponent(child, new RelationshipComponent());

        ref var childRel = ref registry.GetComponent<RelationshipComponent>(child);

        if (childRel.Parent.HasValue)
        {
            var oldParent = childRel.Parent.Value;
            ref var oldParentRel = ref registry.GetComponent<RelationshipComponent>(oldParent);
            
            if (oldParentRel.FirstChild?.Id == child.Id)
                oldParentRel.FirstChild = childRel.NextSibling;

            if (childRel.PrevSibling.HasValue)
            {
                ref var prevRel = ref registry.GetComponent<RelationshipComponent>(childRel.PrevSibling.Value);
                prevRel.NextSibling = childRel.NextSibling;
            }

            if (childRel.NextSibling.HasValue)
            {
                ref var nextRel = ref registry.GetComponent<RelationshipComponent>(childRel.NextSibling.Value);
                nextRel.PrevSibling = childRel.PrevSibling;
            }

            oldParentRel.ChildrenCount--;
        }

        childRel.Parent = newParent;
        childRel.NextSibling = null;
        childRel.PrevSibling = null;

        if (newParent.HasValue)
        {
            if (!registry.HasComponent<RelationshipComponent>(newParent.Value))
                registry.AddComponent(newParent.Value, new RelationshipComponent());

            ref var newParentRel = ref registry.GetComponent<RelationshipComponent>(newParent.Value);

            if (newParentRel.FirstChild == null)
            {
                newParentRel.FirstChild = child;
            }
            else
            {
                Entity current = newParentRel.FirstChild.Value;
                var nextSibling = registry.GetComponent<RelationshipComponent>(current).NextSibling;
                while (nextSibling.HasValue)
                {
                    current = nextSibling.Value;
                    nextSibling = registry.GetComponent<RelationshipComponent>(current).NextSibling;
                }
                ref var lastRel = ref registry.GetComponent<RelationshipComponent>(current);
                lastRel.NextSibling = child;
                childRel.PrevSibling = current;
            }
            newParentRel.ChildrenCount++;
        }
    }
}
