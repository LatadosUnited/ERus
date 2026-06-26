using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Commands;

public class EntityEditCommand : IUndoCommand
{
    private readonly Entity _entity;
    private readonly Registry _registry;
    private readonly string _beforeJson;
    private readonly string _afterJson;

    public string Description { get; }

    public EntityEditCommand(Entity entity, Registry registry, string beforeJson, string afterJson, string propertyName)
    {
        _entity = entity;
        _registry = registry;
        _beforeJson = beforeJson;
        _afterJson = afterJson;
        
        // Obtém o nome da entidade para a descrição se possível
        string entityName = $"Entity {entity.Id}";
        if (registry.HasComponent<TagComponent>(entity))
            entityName = registry.GetComponent<TagComponent>(entity).Name;

        Description = $"Alterar {propertyName} de {entityName}";
    }

    public void Execute()
    {
        // Execute aplica o estado "depois". 
        // Como o Execute é chamado quando a edição já ocorreu na UI e o comando é empilhado,
        // não precisamos forçar a sobreposição na primeira vez, apenas se estivermos "Refazendo" (Redo).
        // Mas por segurança, e para o padrão Command, nós reaplicamos.
        SceneSerializer.DeserializeEntityFromJson(_afterJson, _entity, _registry);
    }

    public void Undo()
    {
        // Reverte para o JSON "antes"
        SceneSerializer.DeserializeEntityFromJson(_beforeJson, _entity, _registry);
    }
}
