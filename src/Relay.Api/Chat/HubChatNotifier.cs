using Microsoft.AspNetCore.SignalR;

namespace Relay.Api.Chat;

internal sealed class HubChatNotifier(IHubContext<ChatHub, IChatClient> hub) : IChatNotifier
{
    public Task MessageAvailableAsync(IReadOnlyCollection<string> deviceIds, EnvelopeDto envelope) =>
        deviceIds.Count == 0 ? Task.CompletedTask : hub.Clients.Groups(deviceIds.Select(HubGroups.Device).ToList()).MessageReceived(envelope);

    public Task TickChangedAsync(string userId, TickDto tick) => hub.Clients.Group(HubGroups.User(userId)).TickUpdated(tick);

    public Task PresenceChangedAsync(IReadOnlyCollection<string> userIds, PresenceDto presence) =>
        userIds.Count == 0 ? Task.CompletedTask : hub.Clients.Groups(userIds.Select(HubGroups.User).ToList()).PresenceChanged(presence);
}

internal static class HubGroups
{
    public static string Device(string deviceId) => $"device:{deviceId}";

    public static string User(string userId) => $"user:{userId}";
}
