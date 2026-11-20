using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Relay.Api.Chat;
using Relay.Api.Hosting;
using Relay.Conversations;
using Relay.Conversations.Contracts;
using Relay.Delivery;
using Relay.Delivery.Contracts;
using Relay.Identity;
using Relay.Identity.Contracts;
using Relay.Presence;
using Relay.Support.Tests;

namespace Relay.Chat.Tests;

/// <summary>The real modules and coordinator wired by DI, with a fake push channel and a hand-moved clock.</summary>
internal sealed class ChatHarness : IDisposable
{
    private readonly ServiceProvider _services;

    public ChatHarness()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(Time);
        services.AddLogging(b => b.AddProvider(Logs).SetMinimumLevel(LogLevel.Trace));
        services.AddIdentityModule().AddConversationsModule().AddPresenceModule();
        services.AddDeliveryModule(o => o.ContentKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
        services.AddSingleton<IChatNotifier>(Push);
        services.AddSingleton<ChatMetrics>();
        services.AddSingleton<ChatCoordinator>();
        _services = services.BuildServiceProvider();
        DemoSeeder.Seed(Identity, Conversations);
        Dm = Conversations.OpenDirect("u_thandi", "u_sipho").Conversation!.ConversationId;
    }

    public ManualTimeProvider Time { get; } = new();

    public CapturingLoggerProvider Logs { get; } = new();

    public FakeNotifier Push { get; } = new();

    public string Dm { get; }

    public SessionPrincipal Thandi { get; } = new("u_thandi", "thandi-phone");

    public SessionPrincipal Sipho { get; } = new("u_sipho", "sipho-phone");

    public ChatCoordinator Chat => _services.GetRequiredService<ChatCoordinator>();

    public IConversations Conversations => _services.GetRequiredService<IConversations>();

    public IDelivery Delivery => _services.GetRequiredService<IDelivery>();

    public IIdentity Identity => _services.GetRequiredService<IIdentity>();

    public static string Id(int n) => $"3d2f4a10-0000-4000-8000-{n:D12}";

    public void Dispose()
    {
        _services.Dispose();
        Logs.Dispose();
    }
}

internal sealed class FakeNotifier : IChatNotifier
{
    public List<(string Device, EnvelopeDto Envelope)> Pushed { get; } = [];

    public List<(string User, TickDto Tick)> Ticks { get; } = [];

    public List<(string User, PresenceDto Presence)> Presence { get; } = [];

    public Task MessageAvailableAsync(IReadOnlyCollection<string> deviceIds, EnvelopeDto envelope)
    {
        foreach (var d in deviceIds)
        {
            Pushed.Add((d, envelope));
        }

        return Task.CompletedTask;
    }

    public Task TickChangedAsync(string userId, TickDto tick)
    {
        Ticks.Add((userId, tick));
        return Task.CompletedTask;
    }

    public Task PresenceChangedAsync(IReadOnlyCollection<string> userIds, PresenceDto presence)
    {
        foreach (var u in userIds)
        {
            Presence.Add((u, presence));
        }

        return Task.CompletedTask;
    }
}
