using Relay.Conversations.Contracts;

namespace Relay.Api.Chat;

public sealed record SendMessageRequest(string ConversationId, string ClientMessageId, string Body);

/// <summary>Reply to a send. <c>Error</c> is a stable code (e.g. "not_a_member") when <c>Ok</c> is false.</summary>
public sealed record SendMessageResult(bool Ok, string? Error, string? MessageId, long? Seq, DateTimeOffset? SentAt, string? Status, bool Duplicate);

public sealed record EnvelopeDto(string MessageId, string ConversationId, long Seq, string SenderId, DateTimeOffset SentAt, string Body);

public sealed record TickDto(string ConversationId, string MessageId, string Status, DateTimeOffset At);

public sealed record PresenceDto(string UserId, bool Online, DateTimeOffset? LastSeenAt);

/// <summary>Events the server pushes to phones over the hub.</summary>
public interface IChatClient
{
    Task MessageReceived(EnvelopeDto envelope);

    Task TickUpdated(TickDto tick);

    Task PresenceChanged(PresenceDto presence);
}

/// <summary>Port for pushing to connected devices; the SignalR adapter is <see cref="HubChatNotifier"/>.</summary>
public interface IChatNotifier
{
    Task MessageAvailableAsync(IReadOnlyCollection<string> deviceIds, EnvelopeDto envelope);

    Task TickChangedAsync(string userId, TickDto tick);

    Task PresenceChangedAsync(IReadOnlyCollection<string> userIds, PresenceDto presence);
}

internal static class StatusNames
{
    public static string Of(TickStatus status) => status switch
    {
        TickStatus.Delivered => "delivered",
        TickStatus.Read => "read",
        _ => "sent",
    };
}
