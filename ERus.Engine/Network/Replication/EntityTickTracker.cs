using System.Collections.Generic;

namespace ERus.Engine.Network.Replication;

/// <summary>
/// Controla a ordenação temporal dos pacotes de estado por entidade.
/// Emite os ticks de saída e descarta pacotes de entrada que chegaram fora de ordem
/// (correção de jitter em canal não confiável).
/// </summary>
public sealed class EntityTickTracker
{
    /// <summary>
    /// Janela de tolerância para wrap-around do contador de tick. Uma diferença maior
    /// que isso indica que o contador deu a volta, não que o pacote é antigo.
    /// </summary>
    private const uint WrapAroundThreshold = 1000000;

    private readonly Dictionary<int, uint> _lastReceivedTicks = new();
    private uint _outgoingTick;

    /// <summary>Gera o próximo tick para um pacote de saída.</summary>
    public uint NextOutgoingTick() => ++_outgoingTick;

    /// <summary>
    /// Retorna true se o pacote deve ser descartado por ser mais antigo que o último
    /// aplicado. Quando aceito, o tick passa a ser o novo mais recente da entidade.
    /// </summary>
    public bool ShouldDrop(int networkId, uint tick)
    {
        if (_lastReceivedTicks.TryGetValue(networkId, out uint lastTick))
        {
            if (tick <= lastTick && lastTick - tick < WrapAroundThreshold)
                return true;
        }

        _lastReceivedTicks[networkId] = tick;
        return false;
    }

    /// <summary>Esquece o histórico de uma entidade destruída.</summary>
    public void Forget(int networkId) => _lastReceivedTicks.Remove(networkId);
}
