using System;
using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Inspector;

/// <summary>
/// Desenha a UI de inspecao de um tipo de componente.
/// Cada componente do ECS tem exatamente um drawer, registrado em
/// <see cref="ComponentDrawerRegistry"/>.
/// </summary>
public interface IComponentDrawer
{
    Type ComponentType { get; }

    /// <summary>Desenha o componente se a entidade do contexto o possuir.</summary>
    void Draw(InspectorContext ctx);
}

/// <summary>
/// Base tipada: resolve a presenca do componente, entrega a referencia mutavel ao
/// drawer concreto e aplica a remocao *depois* do desenho — nunca durante, para que
/// a <c>ref</c> do componente permaneca valida enquanto a UI estiver sendo montada.
/// </summary>
public abstract class ComponentDrawer<T> : IComponentDrawer where T : struct, IComponent
{
    public Type ComponentType => typeof(T);

    public void Draw(InspectorContext ctx)
    {
        if (!ctx.Registry.HasComponentByType(ctx.Entity, typeof(T))) return;

        bool removeRequested;
        {
            ref var component = ref ctx.Registry.GetComponent<T>(ctx.Entity);
            removeRequested = DrawComponent(ctx, ref component);
        }

        if (removeRequested)
            ctx.Registry.RemoveComponent<T>(ctx.Entity);
    }

    /// <summary>
    /// Desenha o componente. Retorne <c>true</c> para que a base o remova da entidade.
    /// </summary>
    protected abstract bool DrawComponent(InspectorContext ctx, ref T component);
}
