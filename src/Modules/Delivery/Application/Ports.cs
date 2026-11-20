using Relay.Delivery.Domain;

namespace Relay.Delivery.Application;

/// <summary>Port for held messages and device queues. In memory for slice 1.</summary>
internal interface IDeliveryStore
{
    HeldMessage? Get(string messageId);

    void Add(HeldMessage message);

    void Remove(string messageId);

    IEnumerable<HeldMessage> All();

    DeviceQueue QueueFor(string deviceId);

    DeviceQueue? FindQueue(string deviceId);

    IEnumerable<DeviceQueue> Queues();
}

/// <summary>Encrypts message bodies at rest. Associated data binds a ciphertext to its message ID.</summary>
internal interface IContentProtector
{
    byte[] Protect(string plaintext, string associatedData);

    string Unprotect(byte[] protectedBytes, string associatedData);
}
