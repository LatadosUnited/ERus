using ERus.Engine.Core;
using System;
using System.Reflection;

namespace ERus.Engine.ECS;

public class Scene
{
    public Registry Registry { get; private set; }

    public Scene()
    {
        Registry = new Registry();
        
        // Auto-registro: escaneia o assembly em busca de todos os structs que implementam IComponent
        var registerMethod = typeof(Registry).GetMethod("RegisterComponent")!;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsValueType && !type.IsAbstract && typeof(IComponent).IsAssignableFrom(type) && type != typeof(IComponent))
            {
                registerMethod.MakeGenericMethod(type).Invoke(Registry, null);
            }
        }
    }

    /// <summary>
    /// A entidade que possui a Camera primária (MainCamera) da cena atual.
    /// Retorna null se nenhuma for encontrada.
    /// </summary>
    public Entity? MainCamera 
    {
        get 
        {
            foreach (var entity in Registry.View<CameraComponent>())
            {
                var cam = Registry.GetComponent<CameraComponent>(entity);
                if (cam.IsPrimary) return entity;
            }
            return null;
        }
    }


    public void Clear()
    {
        Registry.Clear();
    }

    // Retorna um clone profundo (Snapshot) da cena atual para restaurar depois do Play.
    public Scene Clone()
    {
        var newScene = new Scene();
        // Copia o Registry profundamente: entidades, IDs, componentes
        newScene.Registry = Registry.Clone();
        return newScene;
    }
}
