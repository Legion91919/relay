using Microsoft.Extensions.DependencyInjection;
using Relay.Identity.Application;
using Relay.Identity.Contracts;

namespace Relay.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddSingleton<IIdentity, IdentityService>();
        return services;
    }
}
