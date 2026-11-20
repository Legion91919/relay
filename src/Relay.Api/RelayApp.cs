using System.Security.Cryptography;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication;
using Relay.Api.Auth;
using Relay.Api.Chat;
using Relay.Api.Endpoints;
using Relay.Api.Hosting;
using Relay.Conversations;
using Relay.Conversations.Contracts;
using Relay.Delivery;
using Relay.Identity;
using Relay.Identity.Contracts;
using Relay.Presence;

namespace Relay.Api;

/// <summary>Composition root. Kept apart from Program so tests can start the real app on a free port.</summary>
public static class RelayApp
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddProblemDetails();
        builder.Services.AddIdentityModule();
        builder.Services.AddConversationsModule();
        builder.Services.AddPresenceModule();
        var isDevelopment = builder.Environment.IsDevelopment();
        builder.Services.AddDeliveryModule(o =>
        {
            builder.Configuration.GetSection("Delivery").Bind(o);
            if (string.IsNullOrEmpty(o.ContentKey) && isDevelopment)
            {
                // Development only: an ephemeral key per process (held messages are in memory anyway).
                o.ContentKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            }
        });

        builder.Services.AddAuthentication(DemoTokenAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DemoTokenAuthenticationHandler>(DemoTokenAuthenticationHandler.SchemeName, null);
        builder.Services.AddAuthorization();
        builder.Services.AddSignalR(o =>
        {
            o.EnableDetailedErrors = false;
            o.MaximumReceiveMessageSize = 32 * 1024;
            o.KeepAliveInterval = TimeSpan.FromSeconds(20);
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        });
        builder.Services.AddSingleton<ChatMetrics>();
        builder.Services.AddSingleton<IChatNotifier, HubChatNotifier>();
        builder.Services.AddSingleton<ChatCoordinator>();
        builder.Services.AddHostedService<RetentionSweeper>();
        builder.Services.AddHealthChecks().AddCheck<ModulesReadyCheck>("modules", tags: ["ready"]);

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
        app.MapRelayApi();
        app.MapHub<ChatHub>("/hubs/chat");

        if (app.Environment.IsDevelopment())
        {
            DemoSeeder.Seed(app.Services.GetRequiredService<IIdentity>(), app.Services.GetRequiredService<IConversations>());
        }

        return app;
    }
}
