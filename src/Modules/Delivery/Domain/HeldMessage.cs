namespace Relay.Delivery.Domain;

/// <summary>
/// A message body held for devices that have not acknowledged it yet. The body is only ever stored encrypted,
/// and the whole record is deleted when the last device acknowledges (or retention runs out).
/// </summary>
internal sealed class HeldMessage(
    string messageId,
    string conversationId,
    long seq,
    string senderId,
    DateTimeOffset sentAt,
    DateTimeOffset heldSince,
    byte[] protectedBody,
    IEnumerable<string> deviceIds)
{
    private readonly HashSet<string> _pendingDevices = new(deviceIds, StringComparer.Ordinal);

    public string MessageId { get; } = messageId;

    public string ConversationId { get; } = conversationId;

    public long Seq { get; } = seq;

    public string SenderId { get; } = senderId;

    public DateTimeOffset SentAt { get; } = sentAt;

    public DateTimeOffset HeldSince { get; } = heldSince;

    public byte[] ProtectedBody { get; } = protectedBody;

    public IReadOnlyCollection<string> PendingDevices => _pendingDevices;

    public bool FullyDelivered => _pendingDevices.Count == 0;

    public bool IsWaitingFor(string deviceId) => _pendingDevices.Contains(deviceId);

    public bool AckBy(string deviceId) => _pendingDevices.Remove(deviceId);

    public bool IsExpired(DateTimeOffset now, TimeSpan retention) => now - HeldSince >= retention;
}
