using System.Diagnostics.Metrics;

namespace Relay.Api.Chat;

/// <summary>Counts and timings only, never content. Export with any OpenTelemetry/Prometheus collector (meter "Relay.Chat").</summary>
public sealed class ChatMetrics : IDisposable
{
    public const string MeterName = "Relay.Chat";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _accepted;
    private readonly Counter<long> _duplicates;
    private readonly Histogram<double> _deliveryLatency;

    public ChatMetrics()
    {
        _accepted = _meter.CreateCounter<long>("relay.messages.accepted");
        _duplicates = _meter.CreateCounter<long>("relay.messages.duplicate_sends");
        _deliveryLatency = _meter.CreateHistogram<double>("relay.delivery.latency", unit: "ms", description: "Sent to first device ack");
    }

    public void Accepted() => _accepted.Add(1);

    public void Duplicate() => _duplicates.Add(1);

    public void Delivered(TimeSpan latency) => _deliveryLatency.Record(latency.TotalMilliseconds);

    public void Dispose() => _meter.Dispose();
}
