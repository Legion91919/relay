using Microsoft.Extensions.Options;
using Relay.Delivery.Contracts;
using Relay.Delivery.Domain;

namespace Relay.Delivery.Application;

internal sealed class DeliveryService(IDeliveryStore store, IContentProtector protector, IOptions<DeliveryOptions> options, TimeProvider time) : IDelivery
{
    private readonly object _gate = new();
    private readonly DeliveryOptions _options = options.Value;

    public BodyProblem ValidateBody(string? body) => MessageBody.Validate(body, _options.MaxBodyLength);

    public bool Enqueue(OutboundMessage message, IReadOnlyCollection<string> deviceIds)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(deviceIds);
        var id = Normalise(message.MessageId);
        var devices = deviceIds.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.Ordinal).ToList();
        if (devices.Count == 0)
        {
            return false;
        }

        var sealedBody = protector.Protect(message.Body, id);
        lock (_gate)
        {
            if (store.Get(id) is not null)
            {
                return false;
            }

            store.Add(new HeldMessage(id, message.ConversationId, message.Seq, message.SenderId, message.SentAt, time.GetUtcNow(), sealedBody, devices));
            foreach (var device in devices)
            {
                store.QueueFor(device).Add(new QueueEntry(id, message.ConversationId, message.Seq));
            }

            return true;
        }
    }

    public IReadOnlyList<Envelope> Pending(string deviceId)
    {
        List<HeldMessage> held;
        lock (_gate)
        {
            if (store.FindQueue(deviceId) is not { } queue)
            {
                return [];
            }

            held = queue.Entries.Select(e => store.Get(e.MessageId)).OfType<HeldMessage>().ToList();
        }

        return held
            .Select(h => new Envelope(h.MessageId, h.ConversationId, h.Seq, h.SenderId, h.SentAt, protector.Unprotect(h.ProtectedBody, h.MessageId)))
            .ToList();
    }

    public IReadOnlyList<string> Acknowledge(string deviceId, IEnumerable<string> messageIds)
    {
        ArgumentNullException.ThrowIfNull(messageIds);
        var acked = new List<string>();
        lock (_gate)
        {
            if (store.FindQueue(deviceId) is not { } queue)
            {
                return acked;
            }

            foreach (var id in messageIds.Select(Normalise).Distinct(StringComparer.Ordinal))
            {
                if (store.Get(id) is not { } held || !held.AckBy(deviceId))
                {
                    continue;
                }

                queue.Remove(id);
                acked.Add(id);
                if (held.FullyDelivered)
                {
                    // Product rule: the server forgets a message as soon as every recipient device has it.
                    store.Remove(id);
                }
            }
        }

        return acked;
    }

    public int PendingCount(string deviceId)
    {
        lock (_gate)
        {
            return store.FindQueue(deviceId)?.Count ?? 0;
        }
    }

    public DeliveryStats Stats()
    {
        lock (_gate)
        {
            return new DeliveryStats(store.All().Count(), store.Queues().Count(q => q.Count > 0));
        }
    }

    public int PurgeExpired()
    {
        lock (_gate)
        {
            var now = time.GetUtcNow();
            var expired = store.All().Where(h => h.IsExpired(now, _options.Retention)).ToList();
            foreach (var held in expired)
            {
                foreach (var device in held.PendingDevices)
                {
                    store.FindQueue(device)?.Remove(held.MessageId);
                }

                store.Remove(held.MessageId);
            }

            return expired.Count;
        }
    }

    private static string Normalise(string id) => (id ?? string.Empty).Trim().ToLowerInvariant();
}
