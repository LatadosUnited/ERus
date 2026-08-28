using System;
using ImGuiNET;
using System.Numerics;
using ERus.Engine.ECS;
using ERus.Engine.Modules;
using ERus.Engine.Scripting;

namespace ERus.Editor.EditorUI.Inspector;

/// <summary>Botao "Add Component" e o popup com os componentes disponiveis.</summary>
public static class AddComponentMenu
{
    private const string PopupId = "AddComponentPopup";

    public static void Draw(InspectorContext ctx)
    {
        if (ImGui.Button($"{FontAwesome.Plus} Add Component", new Vector2(-1, 25)))
            ImGui.OpenPopup(PopupId);

        if (!ImGui.BeginPopup(PopupId)) return;

        MenuEntry(ctx, "Material", () => new MaterialComponent());
        MenuEntry(ctx, "Sprite Renderer", () => new SpriteRendererComponent());
        MenuEntry(ctx, "Camera", () => new CameraComponent());

        if (ImGui.BeginMenu("Physics"))
        {
            MenuEntry(ctx, "Rigidbody", () => new RigidBodyComponent());
            ImGui.Separator();
            MenuEntry(ctx, "Box Collider", () => new BoxColliderComponent());
            MenuEntry(ctx, "Sphere Collider", () => new SphereColliderComponent());
            MenuEntry(ctx, "Capsule Collider", () => new CapsuleColliderComponent());
            MenuEntry(ctx, "Cylinder Collider", () => new CylinderColliderComponent());
            MenuEntry(ctx, "Mesh Collider", () => new MeshColliderComponent());
            ImGui.EndMenu();
        }

        DrawScriptMenu(ctx);

        ImGui.EndPopup();
    }

    /// <summary>
    /// Entrada que so aparece enquanto a entidade nao tiver o componente.
    /// A factory e explicita para garantir que o construtor sem parametros do struct
    /// (que carrega os valores default do componente) seja de fato chamado.
    /// </summary>
    private static void MenuEntry<T>(InspectorContext ctx, string label, Func<T> factory) where T : struct, IComponent
    {
        if (ctx.Registry.HasComponentByType(ctx.Entity, typeof(T))) return;
        if (!ImGui.MenuItem(label)) return;

        ctx.Registry.AddComponent(ctx.Entity, factory());
        ImGui.CloseCurrentPopup();
    }

    private static void DrawScriptMenu(InspectorContext ctx)
    {
        var scriptModule = ctx.Engine.GetModule<ScriptModule>();

        if (scriptModule == null || scriptModule.AvailableScriptTypes.Count == 0)
        {
            ImGui.BeginDisabled();
            ImGui.MenuItem("Script (nenhum disponível)");
            ImGui.EndDisabled();
            return;
        }

        if (!ImGui.BeginMenu("Script")) return;

        foreach (var scriptType in scriptModule.AvailableScriptTypes)
        {
            if (!ImGui.MenuItem(scriptType.Name)) continue;

            if (!ctx.Registry.HasComponentByType(ctx.Entity, typeof(ScriptComponent)))
                ctx.Registry.AddComponent(ctx.Entity, new ScriptComponent());

            ref var scriptComp = ref ctx.Registry.GetComponent<ScriptComponent>(ctx.Entity);
            scriptComp.Scripts.Add(new ScriptData { ScriptTypeName = scriptType.Name });
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndMenu();
    }
}
