using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Relay.Delivery.Application;
using Relay.Delivery.Contracts;
using Relay.Delivery.Infrastructure;

namespace Relay.Delivery;

public static class DeliveryModule
{
    public static IServiceCollection AddDeliveryModule(this IServiceCollection services, Action<DeliveryOptions> configure)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<DeliveryOptions>()
            .Configure(configure)
            .Validate(o => AesContentProtector.IsValidKey(o.ContentKey), "Delivery:ContentKey must be a base64 AES key of 16, 24 or 32 bytes.")
            .Validate(o => o.Retention > TimeSpan.Zero && o.MaxBodyLength > 0, "Delivery retention and max body length must be positive.")
            .ValidateOnStart();
        services.AddSingleton<IContentProtector, AesContentProtector>();
        services.AddSingleton<IDeliveryStore, InMemoryDeliveryStore>();
        services.AddSingleton<IDelivery, DeliveryService>();
        return services;
    }
}
