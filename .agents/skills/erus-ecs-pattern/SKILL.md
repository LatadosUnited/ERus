---
name: erus-ecs-pattern
description: Use this skill when you need to create new ECS (Entity Component System) logic, add new Components, or create new Systems in the ERus Engine.
---

# ERus ECS (Entity Component System) Pattern

The ERus engine uses a custom ECS architecture. Follow these rules when creating new gameplay or engine logic.

## 1. Components
Components are pure data structures (structs) that implement `IComponent`.
- Location: `ERus.Engine/ECS/` or a specific feature folder.
- Always use `struct`, not `class`.

```csharp
namespace ERus.Engine.ECS;

public struct HealthComponent : IComponent
{
    public float CurrentHealth;
    public float MaxHealth;
}
```

## 2. Systems
Systems contain logic and process entities that have specific components. They must implement `ISystem`.
- Location: `ERus.Engine/ECS/` or a specific feature folder.

```csharp
namespace ERus.Engine.ECS;

public class HealthSystem : ISystem
{
    public void Update(Registry registry, float deltaTime)
    {
        var view = registry.View<HealthComponent>();
        foreach (var entity in view)
        {
            ref var health = ref registry.GetComponent<HealthComponent>(entity);
            // Implement logic here
        }
    }
}
```
*Note: Always use `ref var` when getting a component to ensure you are modifying the actual struct in memory, not a copy.*

## 3. Registering the System
Once a system is created, it must be registered so the Engine updates it every frame.
- Add it to the `ECSModule.cs` or inside the `ActiveScene.Registry` setup depending on the context.
- Usually, systems are added to `Scene.cs` or managed by `ECSModule.cs` in its `Update` method.
