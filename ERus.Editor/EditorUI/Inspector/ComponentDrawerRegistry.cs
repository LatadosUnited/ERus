using System.Collections.Generic;
using ERus.Editor.EditorUI.Inspector.Drawers;

namespace ERus.Editor.EditorUI.Inspector;

/// <summary>
/// Ordem em que os componentes aparecem no Inspector.
/// Para expor um componente novo, escreva o drawer e adicione-o aqui — o
/// <see cref="Panels.InspectorWindow"/> nao precisa saber que ele existe.
/// </summary>
public static class ComponentDrawerRegistry
{
    public static readonly IReadOnlyList<IComponentDrawer> Drawers = new IComponentDrawer[]
    {
        new TagDrawer(),
        new TransformDrawer(),
        new MeshDrawer(),
        new MaterialDrawer(),
        new SpriteRendererDrawer(),
        new RigidBodyDrawer(),
        new BoxColliderDrawer(),
        new SphereColliderDrawer(),
        new CapsuleColliderDrawer(),
        new CylinderColliderDrawer(),
        new MeshColliderDrawer(),
        new CameraDrawer(),
        new ScriptDrawer(),
    };
}
