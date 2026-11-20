using Relay.Conversations.Contracts;

namespace Relay.Conversations.Domain;

/// <summary>One recipient's tick for one message. A small state machine: Sent, then Delivered, then Read, never backwards.</summary>
internal sealed class Receipt(string recipientId)
{
    public string RecipientId { get; } = recipientId;

    public TickStatus Status { get; private set; } = TickStatus.Sent;

    public DateTimeOffset? DeliveredAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>Moves the tick forward. Returns false (and changes nothing) if it would stay put or move back.</summary>
    public bool AdvanceTo(TickStatus target, DateTimeOffset at)
    {
        if (target <= Status)
        {
            return false;
        }

        // Reading a message implies it reached the phone, even if the delivered ack was lost or is still in flight.
        DeliveredAt ??= at;
        if (target == TickStatus.Read)
        {
            ReadAt = at;
        }

        Status = target;
        return true;
    }
}
