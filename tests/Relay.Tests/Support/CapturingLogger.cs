using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Relay.Support.Tests;

/// <summary>Collects every formatted log line and every structured value, to prove what is (not) logged.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Lines { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Capturing(this);

    public void Dispose()
    {
    }

    private sealed class Capturing(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> kv ? string.Join(";", kv.Select(p => $"{p.Key}={p.Value}")) : string.Empty;
            owner.Lines.Enqueue($"{formatter(state, exception)} | {values}");
        }
    }
}
