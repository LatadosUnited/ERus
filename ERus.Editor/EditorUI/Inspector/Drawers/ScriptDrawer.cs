using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ImGuiNET;
using ERus.Engine.ECS;
using ERus.Engine.Modules;
using ERus.Engine.Scripting;

namespace ERus.Editor.EditorUI.Inspector.Drawers;

/// <summary>
/// Desenha um bloco por script anexado, expondo os campos publicos via reflexao.
/// Remove o ScriptComponent quando o ultimo script e retirado.
/// </summary>
public sealed class ScriptDrawer : ComponentDrawer<ScriptComponent>
{
    protected override bool DrawComponent(InspectorContext ctx, ref ScriptComponent scriptComp)
    {
        var scriptModule = ctx.Engine.GetModule<ScriptModule>();

        for (int i = 0; i < scriptComp.Scripts.Count; i++)
        {
            var scriptData = scriptComp.Scripts[i];
            string typeName = scriptData.ScriptTypeName ?? "(nenhum)";

            if (!ImGui.CollapsingHeader($"Script: {typeName}##{i}", ImGuiTreeNodeFlags.DefaultOpen))
                continue;

            if (InspectorContext.BeginPropertyTable($"ScriptTable##{i}"))
            {
                DrawScriptFields(scriptModule, scriptData, typeName, i);
                ImGui.EndTable();
            }

            if (InspectorContext.RemoveComponentButton($"Remove Script##{i}"))
            {
                scriptComp.Scripts.RemoveAt(i);
                i--; // Ajustar index apos remocao
            }
        }

        return scriptComp.Scripts.Count == 0;
    }

    private static void DrawScriptFields(ScriptModule? scriptModule, ScriptData scriptData, string typeName, int index)
    {
        if (scriptModule == null || string.IsNullOrEmpty(typeName)) return;

        var scriptType = scriptModule.AvailableScriptTypes.FirstOrDefault(t => t.Name == typeName);
        if (scriptType == null) return;

        foreach (var field in scriptType.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            InspectorContext.PropertyLabel(field.Name);

            string value = scriptData.FieldValues.TryGetValue(field.Name, out var stored) ? stored : "";
            if (TryDrawField(field, $"##{field.Name}_{index}", ref value))
                scriptData.FieldValues[field.Name] = value;

            ImGui.PopItemWidth();
        }
    }

    /// <summary>Desenha o widget correspondente ao tipo do campo. Retorna true se houve edicao.</summary>
    private static bool TryDrawField(FieldInfo field, string id, ref string value)
    {
        if (field.FieldType == typeof(float))
        {
            float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out float f);
            if (!ImGui.DragFloat(id, ref f, 0.1f)) return false;
            value = f.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (field.FieldType == typeof(int))
        {
            int.TryParse(value, out int i);
            if (!ImGui.DragInt(id, ref i)) return false;
            value = i.ToString();
            return true;
        }

        if (field.FieldType == typeof(bool))
        {
            bool.TryParse(value, out bool b);
            if (!ImGui.Checkbox(id, ref b)) return false;
            value = b.ToString();
            return true;
        }

        if (field.FieldType == typeof(string))
        {
            value ??= "";
            return ImGui.InputText(id, ref value, 256);
        }

        ImGui.TextDisabled(field.FieldType.Name);
        return false;
    }
}
