using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Commands;

public enum LifecycleAction
{
    Create,
    Destroy
}

public class EntityLifecycleCommand : IUndoCommand
{
    private readonly Registry _registry;
    private readonly LifecycleAction _action;
    private readonly int _entityId;
    private readonly string _entityJson;
    private readonly string _entityName;

    public string Description { get; }

    // Construtor usado quando deletamos uma entidade (ela já existe, então salvamos seu estado)
    // Construtor usado quando criamos uma entidade (criamos, e logo depois salvamos seu estado limpo)
    public EntityLifecycleCommand(Entity entity, Registry registry, LifecycleAction action)
    {
        _registry = registry;
        _action = action;
        _entityId = entity.Id;
        _entityJson = SceneSerializer.SerializeEntityToJson(entity, registry);

        _entityName = $"Entity {entity.Id}";
        if (registry.HasComponent<TagComponent>(entity))
            _entityName = registry.GetComponent<TagComponent>(entity).Name;

        string verb = action == LifecycleAction.Create ? "Criar" : "Deletar";
        Description = $"{verb} {_entityName}";
    }

    public void Execute()
    {
        if (_action == LifecycleAction.Create)
        {
            // O Execute real da criação!
            // No momento do primeiro execute, a entidade já existe na Engine (foi criada na UI).
            // Em re-execuções (Redo após Undo), ela precisa ser recriada com o mesmo ID se possível.
            // O Registry atualmente recicla IDs, então um Undo de Create seguido de um Redo 
            // não garante o mesmo ID se outras entidades foram criadas. 
            // Para resolver isso de forma robusta, se a entidade não existir, nós recriamos (o Registry fará o melhor possível).
            
            // Verificamos se está viva (se estamos num Redo, ela estará morta)
            bool isAlive = false;
            foreach (var e in _registry.GetLivingEntities())
            {
                if (e.Id == _entityId) { isAlive = true; break; }
            }

            if (!isAlive)
            {
                // Para simplificar, na refatoração atual, como não há SetEntityId,
                // vamos apenas criar uma nova entidade e restaurar o estado JSON em cima dela.
                var newEntity = _registry.CreateEntity();
                SceneSerializer.DeserializeEntityFromJson(_entityJson, newEntity, _registry);
            }
        }
        else if (_action == LifecycleAction.Destroy)
        {
            // Destrói a entidade
            Entity? target = null;
            foreach (var e in _registry.GetLivingEntities())
            {
                if (e.Id == _entityId) { target = e; break; }
            }

            if (target.HasValue)
            {
                _registry.DestroyEntity(target.Value);
            }
        }
    }

    public void Undo()
    {
        if (_action == LifecycleAction.Create)
        {
            // O Undo de criar é deletar
            Entity? target = null;
            foreach (var e in _registry.GetLivingEntities())
            {
                if (e.Id == _entityId) { target = e; break; }
            }

            if (target.HasValue)
            {
                _registry.DestroyEntity(target.Value);
            }
        }
        else if (_action == LifecycleAction.Destroy)
        {
            // O Undo de deletar é recriar com os dados originais
            // Idealmente, deveríamos forçar o ID (_entityId). Como a API do Registry ainda não tem
            // ForceCreateWithId(int id), criaremos um novo e restauraremos o JSON.
            // Para manter a consistência de relacionamentos, em um sistema robusto o ID precisa ser fixo.
            var restoredEntity = _registry.CreateEntity();
            SceneSerializer.DeserializeEntityFromJson(_entityJson, restoredEntity, _registry);
        }
    }
}
