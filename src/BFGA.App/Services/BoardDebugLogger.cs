using System;
using System.IO;
using System.Linq;
using System.Text;

namespace BFGA.App.Services;

public sealed class BoardDebugLogger : IDisposable
{
    private const int RetainedLogFileCount = 5;

    private readonly object _gate = new();
    private StreamWriter? _writer;
    private bool _disposed;

    public string LogPath { get; }

    private BoardDebugLogger(string documentsRoot)
    {
        var logsDirectory = Path.Combine(documentsRoot, "BFGA", "logs");
        Directory.CreateDirectory(logsDirectory);
        LogPath = Path.Combine(logsDirectory, $"board-debug-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.log");
        _writer = new StreamWriter(
            new FileStream(LogPath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };

        PruneOldLogs(logsDirectory, LogPath);
    }

    public static BoardDebugLogger? CreateIfEnabled(string documentsRoot, bool enabled)
    {
        return enabled ? new BoardDebugLogger(documentsRoot) : null;
    }

    public void Write(string eventName, string message)
    {
        lock (_gate)
        {
            if (_disposed || _writer is null)
            {
                return;
            }

            _writer.WriteLine($"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ} [{eventName}] {message}");
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }

    private static void PruneOldLogs(string logsDirectory, string currentLogFilePath)
    {
        var logFiles = Directory.GetFiles(logsDirectory, "*.log")
            .Select(path => new FileInfo(path))
            .OrderByDescending(fileInfo => fileInfo.LastWriteTimeUtc)
            .ThenByDescending(fileInfo => fileInfo.CreationTimeUtc)
            .ThenByDescending(fileInfo => fileInfo.Name, StringComparer.Ordinal)
            .ToList();

        foreach (var fileInfo in logFiles.Where(fileInfo => !string.Equals(fileInfo.FullName, currentLogFilePath, StringComparison.OrdinalIgnoreCase)).Skip(RetainedLogFileCount - 1))
        {
            try
            {
                fileInfo.Delete();
            }
            catch (IOException)
            {
                // Best effort pruning.
            }
            catch (UnauthorizedAccessException)
            {
                // Best effort pruning.
            }
        }
    }
}
