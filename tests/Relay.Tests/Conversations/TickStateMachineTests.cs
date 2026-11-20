using Relay.Conversations.Contracts;
using Relay.Conversations.Domain;
using Xunit;

namespace Relay.Conversations.Tests;

public class TickStateMachineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

    private static ChatMessage NewMessage() =>
        Conversation.Direct("u_thandi", "u_sipho", T0).Append("6f1c1a52-0000-4000-8000-000000000001", "u_thandi", T0);

    [Fact]
    public void NewMessageStartsAsSent()
    {
        Assert.Equal(TickStatus.Sent, NewMessage().Status);
    }

    [Fact]
    public void SentThenDeliveredThenRead()
    {
        var m = NewMessage();

        var delivered = m.MarkDelivered("u_sipho", T0.AddSeconds(1));
        var read = m.MarkRead("u_sipho", T0.AddSeconds(2));

        Assert.NotNull(delivered);
        Assert.Equal(TickStatus.Delivered, delivered!.Status);
        Assert.NotNull(read);
        Assert.Equal(TickStatus.Read, read!.Status);
        Assert.Equal(TickStatus.Read, m.Status);
    }

    [Fact]
    public void TicksNeverMoveBackwards()
    {
        var m = NewMessage();
        m.MarkRead("u_sipho", T0.AddSeconds(2));

        var late = m.MarkDelivered("u_sipho", T0.AddSeconds(3));

        Assert.Null(late);
        Assert.Equal(TickStatus.Read, m.Status);
    }

    [Fact]
    public void RepeatedDeliveredAckIsNotAChange()
    {
        var m = NewMessage();
        m.MarkDelivered("u_sipho", T0.AddSeconds(1));

        Assert.Null(m.MarkDelivered("u_sipho", T0.AddSeconds(2)));
    }

    [Fact]
    public void ReadBeforeDeliveredAckJumpsToReadAndRecordsDelivery()
    {
        var m = NewMessage();

        var change = m.MarkRead("u_sipho", T0.AddSeconds(5));

        Assert.Equal(TickStatus.Read, change!.Status);
        var receipt = Assert.Single(m.Receipts);
        Assert.Equal(T0.AddSeconds(5), receipt.DeliveredAt);
    }

    [Fact]
    public void SenderCannotTickTheirOwnMessage()
    {
        var m = NewMessage();

        Assert.Null(m.MarkDelivered("u_thandi", T0));
        Assert.Null(m.MarkRead("u_thandi", T0));
        Assert.Equal(TickStatus.Sent, m.Status);
    }

    [Theory]
    [InlineData(TickStatus.Sent, TickStatus.Delivered, true)]
    [InlineData(TickStatus.Sent, TickStatus.Read, true)]
    [InlineData(TickStatus.Delivered, TickStatus.Read, true)]
    [InlineData(TickStatus.Delivered, TickStatus.Delivered, false)]
    [InlineData(TickStatus.Read, TickStatus.Delivered, false)]
    [InlineData(TickStatus.Read, TickStatus.Sent, false)]
    public void ReceiptAdvanceOnlyMovesForward(TickStatus from, TickStatus to, bool expectedChange)
    {
        var r = new Receipt("u_sipho");
        if (from != TickStatus.Sent)
        {
            r.AdvanceTo(from, T0);
        }

        Assert.Equal(expectedChange, r.AdvanceTo(to, T0.AddSeconds(1)));
        Assert.Equal(expectedChange ? to : from, r.Status);
    }
}
