using ERus.Engine.ECS;

namespace ERus.Engine.Scripting;

/// <summary>
/// Classe base para scripts de gameplay do usuário.
/// Todo script do usuário deve herdar desta classe e sobrescrever os métodos desejados.
/// 
/// Exemplo:
///   public class PlayerController : ERusScript
///   {
///       public override void Start() { Log("Jogador pronto!"); }
///       public override void Update() { Transform.Position.X += 1f * (float)DeltaTime; }
///   }
/// </summary>
public abstract class ERusScript
{
    // --- Propriedades injetadas pelo ScriptModule antes do Awake() ---

    /// <summary>
    /// A entidade dona deste script.
    /// </summary>
    public Entity Entity { get; internal set; }

    /// <summary>
    /// Acesso ao Registry ECS completo (criar entidades, ler componentes, etc).
    /// </summary>
    public Registry Registry { get; internal set; }

    /// <summary>
    /// Referência ao orquestrador central da Engine.
    /// </summary>
    public Core.Engine Engine { get; internal set; }

    /// <summary>
    /// Tempo em segundos desde o último frame. Atualizado todo frame antes de Update().
    /// </summary>
    public double DeltaTime { get; internal set; }

    // --- Atalhos de conveniência ---

    /// <summary>
    /// Atalho direto para o TransformComponent da entidade dona.
    /// </summary>
    public ref TransformComponent Transform => ref Registry.GetComponent<TransformComponent>(Entity);

    // --- Callbacks do ciclo de vida (override opcional) ---

    /// <summary>
    /// Chamado uma única vez quando o script é instanciado (antes de Start).
    /// Use para inicializações que não dependem de outros scripts.
    /// </summary>
    public virtual void Awake() { }

    /// <summary>
    /// Chamado uma única vez no primeiro frame após Awake.
    /// Use para inicializações que podem depender de outros objetos já existirem.
    /// </summary>
    public virtual void Start() { }

    /// <summary>
    /// Chamado todo frame durante o modo Play.
    /// Coloque aqui a lógica principal do gameplay (movimento, input, IA, etc).
    /// </summary>
    public virtual void Update() { }

    /// <summary>
    /// Chamado quando a entidade é destruída ou o modo Play termina.
    /// Use para limpeza de recursos.
    /// </summary>
    public virtual void OnDestroy() { }

    // --- Estado interno (gerenciado pelo ScriptModule) ---

    /// <summary>
    /// Indica se Start() já foi chamado nesta instância.
    /// </summary>
    internal bool HasStarted { get; set; } = false;

    // --- Utilitários para o script do usuário ---

    /// <summary>
    /// Escreve uma mensagem informativa no Console do editor.
    /// </summary>
    protected void Log(string message)
    {
        ConsoleLog.Log($"[{GetType().Name}] {message}");
    }

    /// <summary>
    /// Escreve um aviso no Console do editor.
    /// </summary>
    protected void LogWarning(string message)
    {
        ConsoleLog.Warn($"[{GetType().Name}] {message}");
    }

    /// <summary>
    /// Escreve um erro no Console do editor.
    /// </summary>
    protected void LogError(string message)
    {
        ConsoleLog.Error($"[{GetType().Name}] {message}");
    }
}
