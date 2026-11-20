using Relay.Conversations.Contracts;
using Relay.Delivery.Contracts;
using Relay.Identity.Contracts;
using Relay.Presence.Contracts;

namespace Relay.Api.Chat;

public enum SendStatus
{
    Accepted,
    Duplicate,
    Invalid,
    NotFound,
    Forbidden,
    Conflict,
}

public sealed record SendOutcome(SendStatus Status, string? Error, MessageReceipt? Receipt)
{
    public SendMessageResult ToResult() => Receipt is { } r && Status is SendStatus.Accepted or SendStatus.Duplicate
        ? new SendMessageResult(true, null, r.MessageId, r.Seq, r.SentAt, StatusNames.Of(r.Status), Status == SendStatus.Duplicate)
        : new SendMessageResult(false, Error, null, null, null, null, false);
}

/// <summary>
/// Application flow across modules (the modules never call each other): Conversations orders and ticks,
/// Delivery holds encrypted bodies per device, Identity knows devices, Presence knows who is connected.
/// Logs carry IDs and counts only, never message content.
/// </summary>
public sealed partial class ChatCoordinator(
    IConversations conversations,
    IDelivery delivery,
    IIdentity identity,
    IPresence presence,
    IChatNotifier notifier,
    ChatMetrics metrics,
    TimeProvider time,
    ILogger<ChatCoordinator> logger)
{
    public const int MaxAckBatch = 500;

    public async Task<SendOutcome> SendAsync(SessionPrincipal sender, string? conversationId, string? clientMessageId, string? body)
    {
        ArgumentNullException.ThrowIfNull(sender);
        switch (delivery.ValidateBody(body))
        {
            case BodyProblem.Required: return new SendOutcome(SendStatus.Invalid, "body_required", null);
            case BodyProblem.TooLong: return new SendOutcome(SendStatus.Invalid, "body_too_long", null);
        }

        var result = conversations.Accept(sender.UserId, conversationId ?? string.Empty, clientMessageId ?? string.Empty);
        switch (result.Outcome)
        {
            case AcceptOutcome.InvalidMessageId: return new SendOutcome(SendStatus.Invalid, "invalid_message_id", null);
            case AcceptOutcome.UnknownConversation: return new SendOutcome(SendStatus.NotFound, "unknown_conversation", null);
            case AcceptOutcome.NotAMember: return new SendOutcome(SendStatus.Forbidden, "not_a_member", null);
            case AcceptOutcome.MessageIdConflict: return new SendOutcome(SendStatus.Conflict, "message_id_in_use", null);
        }

        var receipt = result.Message!;
        var duplicate = result.Outcome == AcceptOutcome.Duplicate;

        // A retry of a message nobody has received yet re-runs the (idempotent) enqueue. That repairs a crash between
        // accept and enqueue, and never re-delivers: once any device acked, the tick is past Sent.
        var queued = false;
        var devices = result.RecipientIds.SelectMany(identity.DevicesOf).Distinct(StringComparer.Ordinal).ToList();
        if (!duplicate || receipt.Status == TickStatus.Sent)
        {
            queued = delivery.Enqueue(new OutboundMessage(receipt.MessageId, receipt.ConversationId, receipt.Seq, receipt.SenderId, receipt.SentAt, body!), devices);
        }

        if (duplicate)
        {
            metrics.Duplicate();
            LogDuplicate(logger, receipt.MessageId, receipt.ConversationId);
        }
        else
        {
            metrics.Accepted();
            LogAccepted(logger, receipt.MessageId, receipt.ConversationId, receipt.Seq, devices.Count);
        }

        if (queued)
        {
            await notifier.MessageAvailableAsync(devices, new EnvelopeDto(receipt.MessageId, receipt.ConversationId, receipt.Seq, receipt.SenderId, receipt.SentAt, body!));
        }

        return new SendOutcome(duplicate ? SendStatus.Duplicate : SendStatus.Accepted, null, receipt);
    }

    /// <summary>Messages waiting for this device, in order. Called on every (re)connect.</summary>
    public IReadOnlyList<EnvelopeDto> Pending(SessionPrincipal device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return delivery.Pending(device.DeviceId)
            .Select(e => new EnvelopeDto(e.MessageId, e.ConversationId, e.Seq, e.SenderId, e.SentAt, e.Body))
            .ToList();
    }

    public async Task<int> AckDeliveredAsync(SessionPrincipal device, IReadOnlyCollection<string> messageIds)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(messageIds);
        var acked = delivery.Acknowledge(device.DeviceId, messageIds.Take(MaxAckBatch));
        var changes = conversations.MarkDelivered(device.UserId, acked);
        foreach (var change in changes)
        {
            metrics.Delivered(change.At - change.SentAt);
            await notifier.TickChangedAsync(change.SenderId, ToTick(change));
        }

        return acked.Count;
    }

    public async Task<int> MarkReadAsync(SessionPrincipal reader, string conversationId, long upToSeq)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var changes = conversations.MarkReadUpTo(reader.UserId, conversationId, upToSeq);
        foreach (var change in changes)
        {
            await notifier.TickChangedAsync(change.SenderId, ToTick(change));
        }

        return changes.Count;
    }

    public async Task ConnectedAsync(SessionPrincipal device, string connectionId)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (presence.Connected(device.UserId, device.DeviceId, connectionId) is { } status)
        {
            await notifier.PresenceChangedAsync(PartnersOf(device.UserId), ToDto(status));
        }
    }

    public async Task DisconnectedAsync(string connectionId)
    {
        if (presence.Disconnected(connectionId) is { } status)
        {
            await notifier.PresenceChangedAsync(PartnersOf(status.UserId), ToDto(status));
        }
    }

    public DateTimeOffset Now() => time.GetUtcNow();

    private List<string> PartnersOf(string userId) =>
        conversations.ListFor(userId).SelectMany(c => c.MemberIds).Where(m => m != userId).Distinct(StringComparer.Ordinal).ToList();

    private static TickDto ToTick(TickChange c) => new(c.ConversationId, c.MessageId, StatusNames.Of(c.Status), c.At);

    private static PresenceDto ToDto(PresenceStatus s) => new(s.UserId, s.Online, s.LastSeenAt);

    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Message {MessageId} accepted in {ConversationId} as seq {Seq}; queued for {DeviceCount} device(s)")]
    private static partial void LogAccepted(ILogger logger, string messageId, string conversationId, long seq, int deviceCount);

    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "Duplicate send of {MessageId} in {ConversationId} ignored")]
    private static partial void LogDuplicate(ILogger logger, string messageId, string conversationId);
}
