using Relay.Delivery.Contracts;

namespace Relay.Api.Hosting;

/// <summary>Deletes undelivered message bodies older than the retention period (30 days), hourly.</summary>
internal sealed partial class RetentionSweeper(IDelivery delivery, TimeProvider time, ILogger<RetentionSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var purged = delivery.PurgeExpired();
                if (purged > 0)
                {
                    LogPurged(logger, purged);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    [LoggerMessage(EventId = 200, Level = LogLevel.Information, Message = "Retention sweep deleted {Count} undelivered message(s)")]
    private static partial void LogPurged(ILogger logger, int count);
}
