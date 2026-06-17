using System.Collections.Generic;

namespace ERus.Engine.ECS;

/// <summary>
/// Dados de um script de gameplay individual anexado a uma entidade.
/// </summary>
public class ScriptData
{
    public string ScriptTypeName { get; set; } = "";
    public Dictionary<string, string> FieldValues { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// Componente que marca uma entidade como portadora de um ou mais scripts de gameplay.
/// Armazena apenas os nomes dos tipos e propriedades; as instâncias vivas do ERusScript são gerenciadas pelo ScriptExecutionSystem.
/// </summary>
public struct ScriptComponent : IComponent
{
    private List<ScriptData>? _scripts;

    /// <summary>
    /// Lista de scripts anexados a esta entidade.
    /// </summary>
    public List<ScriptData> Scripts
    {
        get
        {
            if (_scripts == null) _scripts = new List<ScriptData>();
            return _scripts;
        }
        set => _scripts = value;
    }
}
