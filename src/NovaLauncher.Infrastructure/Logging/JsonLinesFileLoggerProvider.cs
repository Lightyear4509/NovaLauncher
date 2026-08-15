using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NovaLauncher.Infrastructure.Logging;

public sealed class JsonLinesFileLoggerProvider : ILoggerProvider
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    public JsonLinesFileLoggerProvider(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _writer = new StreamWriter(
            new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
    }

    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new JsonLinesFileLogger(categoryName, Write);
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

    private void Write(LogRecord record)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _writer.WriteLine(JsonSerializer.Serialize(record, LoggingJsonContext.Default.LogRecord));
        }
    }

    internal sealed record LogRecord(
        DateTimeOffset TimestampUtc,
        string Level,
        string Category,
        int EventId,
        string Message,
        string? ExceptionType);

    private sealed class JsonLinesFileLogger(
        string categoryName,
        Action<LogRecord> write) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (!IsEnabled(logLevel))
            {
                return;
            }

            write(new LogRecord(
                DateTimeOffset.UtcNow,
                logLevel.ToString(),
                categoryName,
                eventId.Id,
                formatter(state, exception),
                exception?.GetType().FullName));
        }
    }
}
