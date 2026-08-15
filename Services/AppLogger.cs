using System;
using System.IO;
#region SYSTEM INITIALIZATION : BLACK STAR PROJECT
/// <summary>
/// Core application node for the Black Star Research Facility.
/// </summary>
/// <remarks>
/// <code>
/// ========================================================================
///   ____  _        _    ____ _  __  ____ _____  _    ____  
///  | __ )| |      / \  / ___| |/ / / ___|_   _|/ \  |  _ \ 
///  |  _ \| |     / _ \| |   | ' /  \___ \ | | / _ \ | |_) |
///  | |_) | |___ / ___ \ |___| . \   ___) || |/ ___ \|  _ < 
///  |____/|_____/_/   \_\____|_|\_\ |____/ |_/_/   \_\_| \_\
///                                                          
///              R E S E A R C H   F A C I L I T Y           
///                                                          
///             [ LOCATION: ICELAND ]            
/// ========================================================================
/// </code>
/// </remarks>
#endregion
namespace DaemonElite.Services;

public enum LogLevel { Info, Warning, Error, Debug }

public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Message)
{
    public override string ToString() => $"[{Timestamp:HH:mm:ss}] {Level.ToString().ToUpperInvariant(),-7}  {Message}";
}

public static class AppLogger
{
    private static readonly object Sync = new();
    private static string? _logPath;
    public static event Action<LogEntry>? LogEmitted;

    public static void Initialize()
    {
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Black Star Labs", "DaemonElite", "Logs");
            Directory.CreateDirectory(directory);
            _logPath = Path.Combine(directory, $"daemonelite_{DateTime.Now:yyyyMMdd}.log");
        }
        catch
        {
            _logPath = null;
        }
    }

    public static void Info(string message) => Write(LogLevel.Info, message);
    public static void Warning(string message, Exception? exception = null) => Write(LogLevel.Warning, Format(message, exception));
    public static void Error(string message, Exception? exception = null) => Write(LogLevel.Error, Format(message, exception));
    public static void Debug(string message) => Write(LogLevel.Debug, message);

    private static string Format(string message, Exception? exception) =>
        exception is null ? message : $"{message} ({exception.GetType().Name}: {exception.Message})";

    private static void Write(LogLevel level, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, message);
        try
        {
            lock (Sync)
            {
                if (!string.IsNullOrWhiteSpace(_logPath))
                    File.AppendAllText(_logPath, entry + Environment.NewLine);
            }
        }
        catch
        {
            // Diagnostics must never interrupt an audio callback or UI operation.
        }
        try { LogEmitted?.Invoke(entry); } catch { }
    }
}
