namespace ERus.Engine.Core;

/// <summary>
/// Contrato base para todos os módulos do motor. 
/// Garante que cada subsistema (Gráficos, UI, Rede, Física) obedeça a um ciclo de vida padronizado.
/// </summary>
public interface IEngineModule
{
    /// <summary>
    /// Chamado uma vez durante a inicialização do Engine.
    /// Permite que o módulo reserve memória ou conecte-se a bibliotecas externas.
    /// </summary>
    /// <param name="engine">Referência ao orquestrador central.</param>
    void Initialize(Engine engine);

    /// <summary>
    /// Chamado todo quadro (Frame). Usado para lógicas não visuais (Física, Input, Rede).
    /// </summary>
    /// <param name="deltaTime">Tempo em segundos desde o último quadro.</param>
    void Update(double deltaTime);

    /// <summary>
    /// Chamado logo após o Update. Destinado exclusivamente à renderização gráfica (OpenGL, ImGui).
    /// </summary>
    /// <param name="deltaTime">Tempo em segundos desde o último quadro.</param>
    void Render(double deltaTime);

    /// <summary>
    /// Chamado quando o Engine está sendo desligado.
    /// Desaloca recursos nativos.
    /// </summary>
    void Dispose();
}
