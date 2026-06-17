using ERus.Engine.Core;
using System;

namespace ERus.Engine.ECS;

public class Scene
{
    public Registry Registry { get; private set; }

    public Scene()
    {
        Registry = new Registry();
        
        // Registrar componentes essenciais
        Registry.RegisterComponent<TransformComponent>();
        Registry.RegisterComponent<TagComponent>();
        Registry.RegisterComponent<MeshComponent>();
        Registry.RegisterComponent<NetworkIdentityComponent>();
        Registry.RegisterComponent<CameraComponent>();
        Registry.RegisterComponent<ScriptComponent>();
        Registry.RegisterComponent<RelationshipComponent>();
    }

    public void Clear()
    {
        Registry.Clear();
    }

    // Retorna um clone profundo (Snapshot) da cena atual para restaurar depois do Play.
    public Scene Clone()
    {
        var newScene = new Scene();
        // Cópia profunda do Registry aqui no futuro
        return newScene;
    }
}
