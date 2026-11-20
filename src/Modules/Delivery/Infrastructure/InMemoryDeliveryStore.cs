using Relay.Delivery.Application;
using Relay.Delivery.Domain;

namespace Relay.Delivery.Infrastructure;

/// <summary>Slice-1 store. Not thread-safe on its own: <see cref="DeliveryService"/> serialises access.</summary>
internal sealed class InMemoryDeliveryStore : IDeliveryStore
{
    private readonly Dictionary<string, HeldMessage> _held = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DeviceQueue> _queues = new(StringComparer.Ordinal);

    public HeldMessage? Get(string messageId) => _held.GetValueOrDefault(messageId);

    public void Add(HeldMessage message) => _held[message.MessageId] = message;

    public void Remove(string messageId) => _held.Remove(messageId);

    public IEnumerable<HeldMessage> All() => _held.Values;

    public DeviceQueue QueueFor(string deviceId)
    {
        if (!_queues.TryGetValue(deviceId, out var queue))
        {
            queue = new DeviceQueue(deviceId);
            _queues[deviceId] = queue;
        }

        return queue;
    }

    public DeviceQueue? FindQueue(string deviceId) => _queues.GetValueOrDefault(deviceId);

    public IEnumerable<DeviceQueue> Queues() => _queues.Values;
}
