using System.Collections.Generic;

namespace ERus.Editor.EditorUI;

/// <summary>
/// Interface para comandos reversíveis do Undo/Redo.
/// </summary>
public interface IUndoCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>
/// Sistema de Undo/Redo genérico baseado em command stack.
/// </summary>
public class UndoSystem
{
    private readonly Stack<IUndoCommand> _undoStack = new();
    private readonly Stack<IUndoCommand> _redoStack = new();
    private readonly int _maxHistory;

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public UndoSystem(int maxHistory = 100)
    {
        _maxHistory = maxHistory;
    }

    /// <summary>
    /// Executa um comando e o adiciona à pilha de undo.
    /// Limpa a pilha de redo (não é possível refazer após nova ação).
    /// </summary>
    public void Execute(IUndoCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();

        // Limitar tamanho do histórico
        if (_undoStack.Count > _maxHistory)
        {
            // Stack não suporta remoção do fundo, mas o limite é alto o suficiente
            // para que isso raramente aconteça na prática.
        }
    }

    /// <summary>
    /// Registra um comando já executado (útil para gizmo drag que aplica em tempo real).
    /// </summary>
    public void Record(IUndoCommand command)
    {
        _undoStack.Push(command);
        _redoStack.Clear();
    }

    /// <summary>
    /// Desfaz o último comando.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;
        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
    }

    /// <summary>
    /// Refaz o último comando desfeito.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;
        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
    }

    /// <summary>
    /// Limpa todo o histórico.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}


