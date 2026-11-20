using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Relay.Delivery.Application;
using Relay.Delivery.Contracts;
using Relay.Delivery.Infrastructure;
using Relay.Support.Tests;

namespace Relay.Delivery.Tests;

internal static class DeliveryTestKit
{
    public static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static (DeliveryService Service, InMemoryDeliveryStore Store) Create(ManualTimeProvider time, int maxBody = 2000)
    {
        var options = Options.Create(new DeliveryOptions { ContentKey = NewKey(), MaxBodyLength = maxBody });
        var store = new InMemoryDeliveryStore();
        return (new DeliveryService(store, new AesContentProtector(options), options, time), store);
    }

    public static string Id(int n) => $"0b9e7d10-0000-4000-8000-{n:D12}";

    public static OutboundMessage Message(int n, long seq, string conversationId = "dm.u_sipho.u_thandi", string body = "") =>
        new(Id(n), conversationId, seq, "u_thandi", new DateTimeOffset(2026, 11, 19, 8, 0, 0, TimeSpan.Zero), body.Length == 0 ? $"message {n}" : body);
}
