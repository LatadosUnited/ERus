using System;
using System.IO;
using System.Text;

namespace ERus.Hub;

public class MultiTextWriter : TextWriter
{
    private readonly TextWriter _originalOut;
    private readonly StreamWriter _fileOut;

    public MultiTextWriter(TextWriter originalOut, StreamWriter fileOut)
    {
        _originalOut = originalOut;
        _fileOut = fileOut;
        _fileOut.AutoFlush = true;
    }

    public override Encoding Encoding => _originalOut.Encoding;

    public override void Write(char value)
    {
        _originalOut.Write(value);
        _fileOut.Write(value);
    }

    public override void Write(string? value)
    {
        _originalOut.Write(value);
        _fileOut.Write(value);
    }

    public override void WriteLine(string? value)
    {
        string timestamped = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {value}";
        _originalOut.WriteLine(timestamped);
        _fileOut.WriteLine(timestamped);
    }
}

public static class Logger
{
    public static void Initialize()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string logDir = Path.Combine(appData, "ERusHub", "Logs");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            string logFile = Path.Combine(logDir, "hub.log");
            
            // Backup old log if exists and exceeds 1MB maybe? Let's just append or recreate for simplicity.
            // Recreating is safer for simple launchers.
            var streamWriter = new StreamWriter(logFile, append: false, Encoding.UTF8);
            
            var multiWriter = new MultiTextWriter(Console.Out, streamWriter);
            Console.SetOut(multiWriter);
            Console.SetError(multiWriter);
            
            Console.WriteLine("Logger initialized.");
        }
        catch (Exception ex)
        {
            // Fallback
            Console.WriteLine($"Failed to initialize logger: {ex.Message}");
        }
    }
}
