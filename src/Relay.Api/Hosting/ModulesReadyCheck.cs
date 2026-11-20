using Microsoft.Extensions.Diagnostics.HealthChecks;
using Relay.Delivery.Contracts;
using Relay.Identity.Contracts;

namespace Relay.Api.Hosting;

/// <summary>Ready when the modules answer. Slice 2 adds the database check here.</summary>
internal sealed class ModulesReadyCheck(IIdentity identity, IDelivery delivery) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var stats = delivery.Stats();
        var data = new Dictionary<string, object> { ["users"] = identity.ListUsers().Count, ["messagesHeld"] = stats.MessagesHeld };
        return Task.FromResult(HealthCheckResult.Healthy("Modules responding", data));
    }
}
