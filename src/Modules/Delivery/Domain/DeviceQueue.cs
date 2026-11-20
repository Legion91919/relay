namespace Relay.Delivery.Domain;

internal sealed record QueueEntry(string MessageId, string ConversationId, long Seq);

/// <summary>
/// What one device still has to receive. Within a conversation entries are always in sequence order (a late
/// arrival slots in before higher sequence numbers); across conversations they keep arrival order.
/// </summary>
internal sealed class DeviceQueue(string deviceId)
{
    private readonly List<QueueEntry> _entries = [];

    public string DeviceId { get; } = deviceId;

    public IReadOnlyList<QueueEntry> Entries => _entries;

    public int Count => _entries.Count;

    public bool Add(QueueEntry entry)
    {
        if (_entries.Exists(e => string.Equals(e.MessageId, entry.MessageId, StringComparison.Ordinal)))
        {
            return false;
        }

        var at = _entries.FindIndex(e => string.Equals(e.ConversationId, entry.ConversationId, StringComparison.Ordinal) && e.Seq > entry.Seq);
        if (at < 0)
        {
            _entries.Add(entry);
        }
        else
        {
            _entries.Insert(at, entry);
        }

        return true;
    }

    public bool Remove(string messageId) =>
        _entries.RemoveAll(e => string.Equals(e.MessageId, messageId, StringComparison.Ordinal)) > 0;
}
