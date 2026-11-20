using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Relay.Presence.Application;
using Relay.Presence.Contracts;

namespace Relay.Presence;

public static class PresenceModule
{
    public static IServiceCollection AddPresenceModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPresence, PresenceService>();
        return services;
    }
}
