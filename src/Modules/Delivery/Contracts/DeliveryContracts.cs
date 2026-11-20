namespace Relay.Delivery.Contracts;

/// <summary>A message accepted by Conversations, handed to Delivery to hold until each recipient device has it.</summary>
public sealed record OutboundMessage(string MessageId, string ConversationId, long Seq, string SenderId, DateTimeOffset SentAt, string Body);

/// <summary>What a device receives. Same shape as <see cref="OutboundMessage"/>, body decrypted for the device.</summary>
public sealed record Envelope(string MessageId, string ConversationId, long Seq, string SenderId, DateTimeOffset SentAt, string Body);

/// <summary>Counts only: safe to expose and log.</summary>
public sealed record DeliveryStats(int MessagesHeld, int DevicesWithPending);

public enum BodyProblem
{
    None,
    Required,
    TooLong,
}

public sealed class DeliveryOptions
{
    /// <summary>Undelivered messages are deleted after this long (product rule: 30 days).</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Longest message body in characters.</summary>
    public int MaxBodyLength { get; set; } = 2000;

    /// <summary>Base64 AES key (16, 24 or 32 bytes) that encrypts held message bodies at rest. Required.</summary>
    public string ContentKey { get; set; } = string.Empty;
}

/// <summary>
/// Public entry point of the Delivery module: a per-device queue that holds encrypted message bodies only until
/// every recipient device acknowledges them, then deletes the body.
/// </summary>
public interface IDelivery
{
    BodyProblem ValidateBody(string? body);

    /// <summary>Queues the message for each device. Idempotent per message ID: returns false if it was already held.</summary>
    bool Enqueue(OutboundMessage message, IReadOnlyCollection<string> deviceIds);

    /// <summary>Everything still waiting for this device: per conversation in sequence order, otherwise in arrival order. Does not remove.</summary>
    IReadOnlyList<Envelope> Pending(string deviceId);

    /// <summary>The device has these messages. Returns the IDs that were actually waiting for it.</summary>
    IReadOnlyList<string> Acknowledge(string deviceId, IEnumerable<string> messageIds);

    int PendingCount(string deviceId);

    DeliveryStats Stats();

    /// <summary>Deletes messages held longer than the retention period. Returns how many were deleted.</summary>
    int PurgeExpired();
}
