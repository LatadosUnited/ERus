using System;
using System.Collections.Generic;

namespace ERus.Engine.Core;

/// <summary>
/// Barramento central de eventos. Permite que sistemas publiquem e assinem eventos 
/// desacoplando emissores e receptores (ex: Física e Áudio/Scripts).
/// </summary>
public class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    /// <summary>
    /// Assina um evento de tipo específico.
    /// </summary>
    public void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type))
        {
            _handlers[type] = new List<Delegate>();
        }
        _handlers[type].Add(handler);
    }

    /// <summary>
    /// Remove a assinatura de um evento.
    /// </summary>
    public void Unsubscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (_handlers.TryGetValue(type, out var list))
        {
            list.Remove(handler);
            if (list.Count == 0)
                _handlers.Remove(type);
        }
    }

    /// <summary>
    /// Dispara um evento para todos os inscritos.
    /// </summary>
    public void Publish<T>(T evt)
    {
        var type = typeof(T);
        if (_handlers.TryGetValue(type, out var list))
        {
            // Criamos uma cópia para permitir que handlers façam unubscribe durante a iteração
            var handlersToInvoke = list.ToArray();
            foreach (var handler in handlersToInvoke)
            {
                if (handler is Action<T> action)
                {
                    action.Invoke(evt);
                }
            }
        }
    }

    /// <summary>
    /// Limpa todas as assinaturas (útil ao mudar de cena ou desligar a engine).
    /// </summary>
    public void Clear()
    {
        _handlers.Clear();
    }
}
