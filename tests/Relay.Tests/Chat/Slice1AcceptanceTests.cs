using Relay.Api.Chat;
using Xunit;
using static Relay.Chat.Tests.ChatHarness;

namespace Relay.Chat.Tests;

/// <summary>One test per slice-1 acceptance criterion (backlog.md), through the real modules.</summary>
public class Slice1AcceptanceTests
{
    [Fact]
    public async Task BothOnline_ASends_BReceivesLive_AndASeesDelivered()
    {
        using var h = new ChatHarness();

        var sent = await h.Chat.SendAsync(h.Thandi, h.Dm, Id(1), "Sawubona!");

        Assert.Equal(SendStatus.Accepted, sent.Status);
        Assert.Equal("sent", sent.ToResult().Status);
        var (device, envelope) = Assert.Single(h.Push.Pushed);
        Assert.Equal("sipho-phone", device);
        Assert.Equal("Sawubona!", envelope.Body);

        h.Time.Advance(TimeSpan.FromMilliseconds(250));
        await h.Chat.AckDeliveredAsync(h.Sipho, new[] { envelope.MessageId });

        var (user, tick) = Assert.Single(h.Push.Ticks);
        Assert.Equal("u_thandi", user);
        Assert.Equal("delivered", tick.Status);
        Assert.Equal(Id(1), tick.MessageId);
    }

    [Fact]
    public async Task BOffline_WhenBReconnects_BReceivesMissedMessagesInOrder()
    {
        using var h = new ChatHarness();
        for (var i = 1; i <= 4; i++)
        {
            await h.Chat.SendAsync(h.Thandi, h.Dm, Id(i), $"missed {i}");
        }

        // Sipho's phone was offline: live pushes went nowhere. On reconnect the client syncs the queue.
        var drained = h.Chat.Pending(h.Sipho);

        Assert.Equal(new long[] { 1, 2, 3, 4 }, drained.Select(e => e.Seq).ToArray());
        Assert.Equal(new[] { "missed 1", "missed 2", "missed 3", "missed 4" }, drained.Select(e => e.Body).ToArray());

        await h.Chat.AckDeliveredAsync(h.Sipho, drained.Select(e => e.MessageId).ToArray());

        Assert.Empty(h.Chat.Pending(h.Sipho));
        Assert.Equal(new[] { Id(1), Id(2), Id(3), Id(4) }, h.Push.Ticks.Select(t => t.Tick.MessageId).ToArray());
        Assert.All(h.Push.Ticks, t => Assert.Equal("delivered", t.Tick.Status));
    }

    [Fact]
    public async Task BOpensTheChat_ASeesRead()
    {
        using var h = new ChatHarness();
        await h.Chat.SendAsync(h.Thandi, h.Dm, Id(1), "one");
        await h.Chat.SendAsync(h.Thandi, h.Dm, Id(2), "two");
        await h.Chat.AckDeliveredAsync(h.Sipho, new[] { Id(1), Id(2) });
        h.Push.Ticks.Clear();

        var changed = await h.Chat.MarkReadAsync(h.Sipho, h.Dm, upToSeq: 2);

        Assert.Equal(2, changed);
        Assert.All(h.Push.Ticks, t => Assert.Equal(("u_thandi", "read"), (t.User, t.Tick.Status)));
        Assert.All(h.Conversations.ListReceipts("u_thandi", h.Dm, 0, 10)!, r => Assert.Equal(Relay.Conversations.Contracts.TickStatus.Read, r.Status));
    }

    [Fact]
    public async Task ClientRetriesWithSameMessageId_OnlyOneMessageExists()
    {
        using var h = new ChatHarness();

        var first = await h.Chat.SendAsync(h.Thandi, h.Dm, Id(1), "Are you coming?");
        var retry = await h.Chat.SendAsync(h.Thandi, h.Dm, Id(1), "Are you coming?");

        Assert.Equal(SendStatus.Accepted, first.Status);
        Assert.Equal(SendStatus.Duplicate, retry.Status);
        Assert.True(retry.ToResult().Duplicate);
        Assert.Equal(first.Receipt!.Seq, retry.Receipt!.Seq);
        Assert.Single(h.Conversations.ListReceipts("u_thandi", h.Dm, 0, 10)!);
        Assert.Single(h.Chat.Pending(h.Sipho));
        Assert.Single(h.Push.Pushed);
    }

    [Fact]
    public async Task RetryAfterDeliveryDoesNotDeliverAgain()
    {
        using var h = new ChatHarness();
        await h.Chat.SendAsync(h.Thandi, h.Dm, Id(1), "hi");
        await h.Chat.AckDeliveredAsync(h.Sipho, new[] { Id(1) });

        var late = await h.Chat.SendAsync(h.Thandi, h.Dm, Id(1), "hi");

        Assert.Equal(SendStatus.Duplicate, late.Status);
        Assert.Equal("delivered", late.ToResult().Status);
        Assert.Empty(h.Chat.Pending(h.Sipho));
        Assert.Equal(0, h.Delivery.Stats().MessagesHeld);
    }

    [Fact]
    public async Task ServerForgetsTheBodyOnceDelivered_ButKeepsReceipts()
    {
        using var h = new ChatHarness();
        await h.Chat.SendAsync(h.Thandi, h.Dm, Id(1), "private words");
        Assert.Equal(1, h.Delivery.Stats().MessagesHeld);

        await h.Chat.AckDeliveredAsync(h.Sipho, new[] { Id(1) });

        Assert.Equal(0, h.Delivery.Stats().MessagesHeld);
        Assert.Single(h.Conversations.ListReceipts("u_thandi", h.Dm, 0, 10)!);
    }

    [Fact]
    public async Task MessageContentNeverReachesTheLogs()
    {
        using var h = new ChatHarness();
        const string Secret = "zebra-marker-9931";

        await h.Chat.SendAsync(h.Thandi, h.Dm, Id(1), Secret);
        await h.Chat.SendAsync(h.Thandi, h.Dm, Id(1), Secret);
        await h.Chat.AckDeliveredAsync(h.Sipho, new[] { Id(1) });
        await h.Chat.MarkReadAsync(h.Sipho, h.Dm, 1);

        Assert.NotEmpty(h.Logs.Lines);
        Assert.Contains(h.Logs.Lines, l => l.Contains(Id(1), StringComparison.Ordinal));
        Assert.DoesNotContain(h.Logs.Lines, l => l.Contains(Secret, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("", SendStatus.Invalid, "body_required")]
    [InlineData("hello", SendStatus.Forbidden, "not_a_member")]
    public async Task RejectedSendsSayWhy(string body, SendStatus status, string error)
    {
        using var h = new ChatHarness();
        var pule = new Relay.Identity.Contracts.SessionPrincipal("u_pule", "pule-phone");

        var outcome = await h.Chat.SendAsync(pule, h.Dm, Id(1), body);

        Assert.Equal(status, outcome.Status);
        Assert.Equal(error, outcome.Error);
        Assert.Empty(h.Push.Pushed);
    }

    [Fact]
    public async Task PresenceChangesAreSentToConversationPartners()
    {
        using var h = new ChatHarness();

        await h.Chat.ConnectedAsync(h.Sipho, "conn-1");
        await h.Chat.DisconnectedAsync("conn-1");

        Assert.Contains(h.Push.Presence, p => p.User == "u_thandi" && p.Presence.Online);
        Assert.Contains(h.Push.Presence, p => p.User == "u_thandi" && !p.Presence.Online && p.Presence.LastSeenAt is not null);
        Assert.DoesNotContain(h.Push.Presence, p => p.User == "u_pule");
    }
}
