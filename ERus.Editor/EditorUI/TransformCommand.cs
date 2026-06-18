using ERus.Engine.ECS;
using ERus.Engine.Modules;
using Silk.NET.Maths;

namespace ERus.Editor.EditorUI;

/// <summary>
/// Comando de Undo/Redo para transformações feitas pelo Gizmo.
/// Armazena o estado antigo e novo do TransformComponent.
/// </summary>
public class TransformCommand : IUndoCommand
{
    private readonly Entity _entity;
    private readonly Registry _registry;
    private readonly NetworkModule? _netModule;

    private readonly Vector3D<float> _oldPosition;
    private readonly Vector3D<float> _oldRotation;
    private readonly Vector3D<float> _oldScale;

    private readonly Vector3D<float> _newPosition;
    private readonly Vector3D<float> _newRotation;
    private readonly Vector3D<float> _newScale;

    public string Description { get; }

    public TransformCommand(
        Entity entity, Registry registry, NetworkModule? netModule,
        Vector3D<float> oldPos, Vector3D<float> oldRot, Vector3D<float> oldScale,
        Vector3D<float> newPos, Vector3D<float> newRot, Vector3D<float> newScale,
        string description = "Transform")
    {
        _entity = entity;
        _registry = registry;
        _netModule = netModule;
        _oldPosition = oldPos;
        _oldRotation = oldRot;
        _oldScale = oldScale;
        _newPosition = newPos;
        _newRotation = newRot;
        _newScale = newScale;
        Description = description;
    }

    public void Execute()
    {
        ApplyTransform(_newPosition, _newRotation, _newScale);
    }

    public void Undo()
    {
        ApplyTransform(_oldPosition, _oldRotation, _oldScale);
    }

    private void ApplyTransform(Vector3D<float> pos, Vector3D<float> rot, Vector3D<float> scale)
    {
        if (!_registry.HasComponent<TransformComponent>(_entity)) return;

        ref var t = ref _registry.GetComponent<TransformComponent>(_entity);
        t.Position = pos;
        t.Rotation = rot;
        t.Scale = scale;

        // Replicar pela rede se necessário
        if (_netModule != null && _registry.HasComponent<NetworkIdentityComponent>(_entity))
        {
            var netId = _registry.GetComponent<NetworkIdentityComponent>(_entity).NetworkId;

        }
    }
}



