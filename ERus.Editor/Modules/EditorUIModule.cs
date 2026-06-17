using ERus.Engine.Core;
using ERus.Editor.EditorUI;

namespace ERus.Editor.Modules;

/// <summary>
/// Módulo que engloba toda a interface do desenvolvedor (ImGui).
/// Responsável por gerenciar os painéis ancorados e a barra de ferramentas.
/// </summary>
public class EditorUIModule : IEngineModule
{
    private EditorUIController _editorUI;
    private ERus.Engine.Core.Engine _engine;
    public EditorUIController EditorUI => _editorUI;

    /// <summary>
    /// Injeta as dependências nativas (Janela, Input, GL) no controlador de UI.
    /// </summary>
    public void Initialize(ERus.Engine.Core.Engine engine)
    {
        _engine = engine;
        _editorUI = new EditorUIController(_engine);
        _editorUI.Initialize(engine.Window, engine.Input, engine.Gl);
    }

    /// <summary>
    /// Computa a matemática dos painéis e atualiza o backend do ImGui.
    /// </summary>
    public void Update(double deltaTime)
    {
        _editorUI.Update(deltaTime);
    }

    /// <summary>
    /// Desenha efetivamente os vértices da UI por cima de tudo no frame.
    /// </summary>
    public void Render(double deltaTime)
    {
        _editorUI.Render();
    }

    public void Dispose()
    {
        _editorUI?.Dispose();
    }
}


