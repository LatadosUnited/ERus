namespace ERus.Engine.Scripting;

/// <summary>
/// Log centralizado para o Console do editor.
/// Scripts do usuário e sistemas internos escrevem aqui;
/// a ConsoleWindow lê e exibe com cores.
/// </summary>
public static class ConsoleLog
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public readonly record struct LogEntry(string Message, LogLevel Level, DateTime Timestamp);

    private static readonly List<LogEntry> _entries = new List<LogEntry>();
    private static readonly object _lock = new object();

    /// <summary>
    /// Número máximo de entradas mantidas na memória.
    /// </summary>
    public const int MaxEntries = 500;

    public static IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock) return _entries.ToList().AsReadOnly();
        }
    }

    public static void Log(string message)
    {
        AddEntry(message, LogLevel.Info);
    }

    public static void Warn(string message)
    {
        AddEntry(message, LogLevel.Warning);
    }

    public static void Error(string message)
    {
        AddEntry(message, LogLevel.Error);
    }

    public static void Clear()
    {
        lock (_lock) _entries.Clear();
    }

    private static void AddEntry(string message, LogLevel level)
    {
        lock (_lock)
        {
            _entries.Add(new LogEntry(message, level, DateTime.Now));
            if (_entries.Count > MaxEntries)
                _entries.RemoveAt(0);
        }
        // Também escreve no stdout para debugging
        Console.WriteLine($"[{level}] {message}");
    }
}
