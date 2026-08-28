using System;

namespace ERus.Engine.Network.Attributes;

/// <summary>
/// Marca um método dentro de um ERusScript para ser executado remotamente no Servidor/Host.
/// Quando invocado por um cliente, um pacote ScriptRpcPacket é enviado para o Host.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class ServerRpcAttribute : Attribute
{
    public bool RequireOwnership { get; set; } = false;
}

/// <summary>
/// Marca um método dentro de um ERusScript para ser executado em todos os Clientes conectados.
/// Quando disparado pelo Host, um pacote ScriptRpcPacket é enviado a todos os peers.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class ClientRpcAttribute : Attribute
{
}

/// <summary>
/// Marca um campo ou propriedade dentro de um ERusScript para ser sincronizado automaticamente pela rede.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class SyncVarAttribute : Attribute
{
    public string? Hook { get; set; }
}
