using System;
using System.IO;
using Microsoft.Extensions.Logging;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Logging;

public static class PosLog
{
    public static string LogDirectory { get; } =
        Path.Combine(
            AppStoragePaths.GetDocumentsFolder(),
            AppStoragePaths.RootFolderName,
            "logs");

    public static string CurrentLogFilePath { get; } =
        Path.Combine(LogDirectory, $"pos-{DateTime.Now:yyyy-MM-dd}.log");
}

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _gate = new();

    public FileLoggerProvider(string filePath) => _filePath = filePath;

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _filePath, _gate);

    public void Dispose()
    {
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly string _filePath;
        private readonly object _gate;

        public FileLogger(string category, string filePath, object gate)
        {
            _category = category;
            _filePath = filePath;
            _gate = gate;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var line =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_category}: {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                lock (_gate)
                {
                    File.AppendAllText(_filePath, line + Environment.NewLine);
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine(line);
            }
        }
    }
}
