using Relay.Conversations.Contracts;

namespace Relay.Conversations.Domain;

/// <summary>Message metadata only (who, when, order, ticks). The body is never stored in this module.</summary>
internal sealed class ChatMessage
{
    private readonly Dictionary<string, Receipt> _receipts;

    public ChatMessage(string id, string conversationId, long seq, string senderId, DateTimeOffset sentAt, IEnumerable<string> recipientIds)
    {
        Id = id;
        ConversationId = conversationId;
        Seq = seq;
        SenderId = senderId;
        SentAt = sentAt;
        _receipts = recipientIds.ToDictionary(r => r, r => new Receipt(r), StringComparer.Ordinal);
    }

    public string Id { get; }

    public string ConversationId { get; }

    public long Seq { get; }

    public string SenderId { get; }

    public DateTimeOffset SentAt { get; }

    public IReadOnlyCollection<Receipt> Receipts => _receipts.Values;

    /// <summary>The tick the sender sees: the lowest status across recipients (a group message is "read" when everyone read it).</summary>
    public TickStatus Status => _receipts.Count == 0 ? TickStatus.Sent : _receipts.Values.Min(r => r.Status);

    public TickChange? MarkDelivered(string recipientId, DateTimeOffset at) => Advance(recipientId, TickStatus.Delivered, at);

    public TickChange? MarkRead(string recipientId, DateTimeOffset at) => Advance(recipientId, TickStatus.Read, at);

    public MessageReceipt ToReceipt() => new(Id, ConversationId, Seq, SenderId, SentAt, Status);

    private TickChange? Advance(string recipientId, TickStatus target, DateTimeOffset at)
    {
        if (!_receipts.TryGetValue(recipientId, out var receipt))
        {
            return null;
        }

        var before = Status;
        if (!receipt.AdvanceTo(target, at))
        {
            return null;
        }

        var after = Status;
        return after > before ? new TickChange(ConversationId, Id, SenderId, after, at, SentAt) : null;
    }
}
