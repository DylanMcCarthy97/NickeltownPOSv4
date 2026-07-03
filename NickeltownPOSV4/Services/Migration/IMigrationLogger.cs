using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Migration;

public interface IMigrationLogger
{
    void LogInformation(string message);

    void LogWarning(string message);

    void LogError(string message);

    Task FlushAsync(CancellationToken cancellationToken = default);
}

public sealed class NullMigrationLogger : IMigrationLogger
{
    public void LogInformation(string message) { }

    public void LogWarning(string message) { }

    public void LogError(string message) { }

    public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class FileMigrationLogger : IMigrationLogger, IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    public FileMigrationLogger(string logFilePath)
    {
        var directory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(new FileStream(logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };
    }

    public void LogInformation(string message) => Write("INFO", message);

    public void LogWarning(string message) => Write("WARN", message);

    public void LogError(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _writer.WriteLine($"{DateTimeOffset.UtcNow:O}\t{level}\t{message}");
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _writer.FlushAsync(cancellationToken);
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
            _writer.Dispose();
        }
    }
}
