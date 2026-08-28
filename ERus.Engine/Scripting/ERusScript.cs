using ERus.Engine.ECS;
using ERus.Engine.Modules;
using System.Numerics;

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

    /// <summary>
    /// Tamanho atual da tela / GameView em pixels.
    /// </summary>
    public Vector2 ScreenSize => Engine.GetModule<GraphicsModule>()?.GameViewSize ?? Vector2.Zero;

    /// <summary>
    /// A entidade que possui a Camera primária (MainCamera) da cena atual.
    /// Retorna null se nenhuma for encontrada.
    /// </summary>
    public Entity? MainCamera => Engine.GetModule<ECSModule>()?.ActiveScene?.MainCamera;

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

    // --- Propriedades de Rede e Colaboração ---

    /// <summary>
    /// Indica se este processo está rodando como Servidor ou Host da sessão de rede.
    /// </summary>
    public bool IsServer => Engine.GetModule<NetworkModule>()?.NetworkManager?.IsHost ?? false;

    /// <summary>
    /// Indica se este processo está conectado como Cliente a um Host remoto.
    /// </summary>
    public bool IsClient => (Engine.GetModule<NetworkModule>()?.NetworkManager?.IsClient ?? false) && !IsServer;

    /// <summary>
    /// Indica se este processo é o Host da sessão de rede.
    /// </summary>
    public bool IsHost => Engine.GetModule<NetworkModule>()?.NetworkManager?.IsHost ?? false;

    /// <summary>
    /// ID de Rede da entidade dona deste script, ou -1 se não possuir NetworkIdentityComponent.
    /// </summary>
    public int NetworkId => Registry.HasComponent<NetworkIdentityComponent>(Entity) 
        ? Registry.GetComponent<NetworkIdentityComponent>(Entity).NetworkId 
        : -1;

    /// <summary>
    /// Indica se o usuário local tem posse/autoridade ou lock sobre a entidade.
    /// </summary>
    public bool IsOwner
    {
        get
        {
            if (IsHost) return true;
            if (!Registry.HasComponent<NetworkIdentityComponent>(Entity)) return false;
            var netId = Registry.GetComponent<NetworkIdentityComponent>(Entity);
            var netModule = Engine.GetModule<NetworkModule>();
            int localUserId = netModule?.NetworkManager?.Transport?.NetManager?.FirstPeer?.Id ?? -1;
            return netId.LockUserId == localUserId || netId.LockUserId == -1;
        }
    }

    /// <summary>
    /// Envia uma chamada RPC para ser executada no Servidor/Host.
    /// </summary>
    public void SendServerRpc(string methodName, params string[] args)
    {
        var netModule = Engine.GetModule<NetworkModule>();
        if (netModule?.NetworkManager == null) return;

        int netId = NetworkId;
        if (netId == -1)
        {
            LogError($"Não é possível enviar ServerRpc '{methodName}': Entidade #{Entity.Id} não possui NetworkIdentityComponent.");
            return;
        }

        var packet = new ERus.Engine.Network.Packets.Events.ScriptRpcPacket
        {
            NetworkId = netId,
            ScriptTypeName = GetType().Name,
            MethodName = methodName,
            IsServerRpc = true,
            Arguments = args
        };

        if (IsHost)
        {
            ExecuteRpcLocal(methodName, args);
        }
        else
        {
            netModule.NetworkManager.Dispatcher.SendToServer(packet, LiteNetLib.DeliveryMethod.ReliableOrdered);
        }
    }

    /// <summary>
    /// Envia uma chamada RPC do Servidor/Host para todos os clientes conectados.
    /// </summary>
    public void SendClientRpc(string methodName, params string[] args)
    {
        var netModule = Engine.GetModule<NetworkModule>();
        if (netModule?.NetworkManager == null) return;

        if (!IsHost)
        {
            LogError($"Apenas o Host pode enviar ClientRpc '{methodName}'.");
            return;
        }

        int netId = NetworkId;
        if (netId == -1) return;

        var packet = new ERus.Engine.Network.Packets.Events.ScriptRpcPacket
        {
            NetworkId = netId,
            ScriptTypeName = GetType().Name,
            MethodName = methodName,
            IsServerRpc = false,
            Arguments = args
        };

        ExecuteRpcLocal(methodName, args);
        netModule.NetworkManager.Dispatcher.SendToAllExcept(packet, null, LiteNetLib.DeliveryMethod.ReliableOrdered);
    }

    /// <summary>
    /// Sincroniza o valor de uma variável SyncVar com a rede.
    /// </summary>
    public void SyncVar(string fieldName, object value)
    {
        var netModule = Engine.GetModule<NetworkModule>();
        if (netModule?.NetworkManager == null) return;

        int netId = NetworkId;
        if (netId == -1) return;

        var packet = new ERus.Engine.Network.Packets.Events.ScriptSyncVarPacket
        {
            NetworkId = netId,
            ScriptTypeName = GetType().Name,
            FieldName = fieldName,
            Value = value?.ToString() ?? string.Empty
        };

        if (IsHost)
        {
            netModule.NetworkManager.Dispatcher.SendToAllExcept(packet, null, LiteNetLib.DeliveryMethod.ReliableOrdered);
        }
        else
        {
            netModule.NetworkManager.Dispatcher.SendToServer(packet, LiteNetLib.DeliveryMethod.ReliableOrdered);
        }
    }

    internal void ExecuteRpcLocal(string methodName, string[] args)
    {
        var method = GetType().GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method == null)
        {
            LogError($"Método RPC '{methodName}' não encontrado no script {GetType().Name}.");
            return;
        }

        var parameters = method.GetParameters();
        object?[] convertedArgs = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var pType = parameters[i].ParameterType;
            string rawVal = i < args.Length ? args[i] : "";

            try
            {
                if (pType == typeof(string)) convertedArgs[i] = rawVal;
                else if (pType == typeof(int) && int.TryParse(rawVal, out int iVal)) convertedArgs[i] = iVal;
                else if (pType == typeof(float) && float.TryParse(rawVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float fVal)) convertedArgs[i] = fVal;
                else if (pType == typeof(bool) && bool.TryParse(rawVal, out bool bVal)) convertedArgs[i] = bVal;
                else if (pType == typeof(double) && double.TryParse(rawVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dVal)) convertedArgs[i] = dVal;
                else convertedArgs[i] = parameters[i].DefaultValue ?? null;
            }
            catch
            {
                convertedArgs[i] = parameters[i].DefaultValue ?? null;
            }
        }

        try
        {
            method.Invoke(this, convertedArgs);
        }
        catch (Exception ex)
        {
            LogError($"Erro ao executar RPC '{methodName}': {ex.InnerException?.Message ?? ex.Message}");
        }
    }

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
