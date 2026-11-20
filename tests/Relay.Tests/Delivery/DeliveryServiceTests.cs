using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Relay.Delivery.Contracts;
using Relay.Delivery.Infrastructure;
using Relay.Support.Tests;
using Xunit;
using static Relay.Delivery.Tests.DeliveryTestKit;

namespace Relay.Delivery.Tests;

public class DeliveryServiceTests
{
    private const string Phone = "sipho-phone";
    private const string Tablet = "sipho-tablet";

    private readonly ManualTimeProvider _time = new();

    [Fact]
    public void OfflineDeviceGetsEverythingItMissedInOrderOnReconnect()
    {
        var (sut, _) = Create(_time);
        for (var i = 1; i <= 3; i++)
        {
            sut.Enqueue(Message(i, seq: i), new[] { Phone });
        }

        var drained = sut.Pending(Phone);

        Assert.Equal(new long[] { 1, 2, 3 }, drained.Select(e => e.Seq).ToArray());
        Assert.Equal(new[] { "message 1", "message 2", "message 3" }, drained.Select(e => e.Body).ToArray());
    }

    [Fact]
    public void MessagesThatArriveOutOfOrderStillDrainInSequenceOrder()
    {
        var (sut, _) = Create(_time);
        sut.Enqueue(Message(1, seq: 1), new[] { Phone });
        sut.Enqueue(Message(3, seq: 3), new[] { Phone });
        sut.Enqueue(Message(2, seq: 2), new[] { Phone });

        Assert.Equal(new long[] { 1, 2, 3 }, sut.Pending(Phone).Select(e => e.Seq).ToArray());
    }

    [Fact]
    public void EnqueueIsIdempotentPerMessageId()
    {
        var (sut, _) = Create(_time);

        Assert.True(sut.Enqueue(Message(1, 1), new[] { Phone }));
        Assert.False(sut.Enqueue(Message(1, 1), new[] { Phone }));
        Assert.Single(sut.Pending(Phone));
        Assert.Equal(1, sut.Stats().MessagesHeld);
    }

    [Fact]
    public void BodyIsDeletedOnlyWhenEveryRecipientDeviceHasAcked()
    {
        var (sut, _) = Create(_time);
        sut.Enqueue(Message(1, 1), new[] { Phone, Tablet });

        sut.Acknowledge(Phone, new[] { Id(1) });

        Assert.Equal(0, sut.PendingCount(Phone));
        Assert.Equal(1, sut.PendingCount(Tablet));
        Assert.Equal(1, sut.Stats().MessagesHeld);

        sut.Acknowledge(Tablet, new[] { Id(1) });

        Assert.Equal(new DeliveryStats(0, 0), sut.Stats());
    }

    [Fact]
    public void AckReturnsOnlyMessagesThatWereWaitingForThatDevice()
    {
        var (sut, _) = Create(_time);
        sut.Enqueue(Message(1, 1), new[] { Phone });
        sut.Enqueue(Message(2, 2), new[] { Phone });

        var first = sut.Acknowledge(Phone, new[] { Id(1), Id(99), "garbage" });
        var again = sut.Acknowledge(Phone, new[] { Id(1) });
        var stranger = sut.Acknowledge("lerato-phone", new[] { Id(2) });

        Assert.Equal(new[] { Id(1) }, first.ToArray());
        Assert.Empty(again);
        Assert.Empty(stranger);
        Assert.Equal(new[] { Id(2) }, sut.Pending(Phone).Select(e => e.MessageId).ToArray());
    }

    [Fact]
    public void PartialAckLeavesTheRestInOrder()
    {
        var (sut, _) = Create(_time);
        for (var i = 1; i <= 5; i++)
        {
            sut.Enqueue(Message(i, i), new[] { Phone });
        }

        sut.Acknowledge(Phone, new[] { Id(2), Id(4) });

        Assert.Equal(new long[] { 1, 3, 5 }, sut.Pending(Phone).Select(e => e.Seq).ToArray());
    }

    [Fact]
    public void AckIsCaseInsensitiveOnMessageIds()
    {
        var (sut, _) = Create(_time);
        sut.Enqueue(Message(1, 1), new[] { Phone });

        Assert.Single(sut.Acknowledge(Phone, new[] { Id(1).ToUpperInvariant() }));
    }

    [Fact]
    public void BodiesAreEncryptedAtRest()
    {
        var (sut, store) = Create(_time);
        sut.Enqueue(Message(1, 1, body: "meet at the taxi rank"), new[] { Phone });

        var held = Assert.Single(store.All());
        var asText = Encoding.UTF8.GetString(held.ProtectedBody);

        Assert.DoesNotContain("taxi", asText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("meet at the taxi rank", Assert.Single(sut.Pending(Phone)).Body);
    }

    [Fact]
    public void UndeliveredMessagesAreDeletedAfterThirtyDays()
    {
        var (sut, _) = Create(_time);
        sut.Enqueue(Message(1, 1), new[] { Phone });

        _time.Advance(TimeSpan.FromDays(29));
        Assert.Equal(0, sut.PurgeExpired());
        Assert.Equal(1, sut.PendingCount(Phone));

        _time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(1, sut.PurgeExpired());
        Assert.Empty(sut.Pending(Phone));
        Assert.Equal(0, sut.Stats().MessagesHeld);
    }

    [Fact]
    public void NoDevicesMeansNothingIsHeld()
    {
        var (sut, _) = Create(_time);

        Assert.False(sut.Enqueue(Message(1, 1), Array.Empty<string>()));
        Assert.Equal(0, sut.Stats().MessagesHeld);
    }

    [Fact]
    public void ValidateBodyUsesTheConfiguredLimit()
    {
        var (sut, _) = Create(_time, maxBody: 5);

        Assert.Equal(BodyProblem.TooLong, sut.ValidateBody("123456"));
        Assert.Equal(BodyProblem.None, sut.ValidateBody("12345"));
        Assert.Equal(BodyProblem.Required, sut.ValidateBody(" "));
    }

    [Fact]
    public async Task ConcurrentAcksFromTwoDevicesDeleteTheBodyExactlyOnce()
    {
        var (sut, _) = Create(_time);
        for (var i = 1; i <= 100; i++)
        {
            sut.Enqueue(Message(i, i), new[] { Phone, Tablet });
        }

        var ids = Enumerable.Range(1, 100).Select(Id).ToArray();
        await Task.WhenAll(
            Task.Run(() => sut.Acknowledge(Phone, ids)),
            Task.Run(() => sut.Acknowledge(Tablet, ids)));

        Assert.Equal(new DeliveryStats(0, 0), sut.Stats());
    }
}

public class ContentProtectorTests
{
    private static AesContentProtector Protector(string key) => new(Options.Create(new DeliveryOptions { ContentKey = key }));

    [Fact]
    public void RoundTrips()
    {
        var p = Protector(NewKey());

        Assert.Equal("Dumela!", p.Unprotect(p.Protect("Dumela!", "m1"), "m1"));
    }

    [Fact]
    public void SameTextEncryptsDifferentlyEachTime()
    {
        var p = Protector(NewKey());

        Assert.False(p.Protect("hi", "m1").AsSpan().SequenceEqual(p.Protect("hi", "m1")));
    }

    [Fact]
    public void CiphertextIsBoundToItsMessageId()
    {
        var p = Protector(NewKey());
        var sealedBody = p.Protect("hi", "m1");

        Assert.Throws<AuthenticationTagMismatchException>(() => p.Unprotect(sealedBody, "m2"));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("not base64!", false)]
    [InlineData("AAAA", false)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==", true)]
    public void KeyValidation(string key, bool valid)
    {
        Assert.Equal(valid, AesContentProtector.IsValidKey(key));
    }
}
