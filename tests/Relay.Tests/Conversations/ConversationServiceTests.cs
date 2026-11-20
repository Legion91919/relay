using Relay.Conversations.Application;
using Relay.Conversations.Contracts;
using Relay.Conversations.Infrastructure;
using Relay.Support.Tests;
using Xunit;

namespace Relay.Conversations.Tests;

public class ConversationServiceTests
{
    private const string Thandi = "u_thandi";
    private const string Sipho = "u_sipho";
    private const string Lerato = "u_lerato";

    private readonly ManualTimeProvider _time = new();
    private readonly ConversationService _sut;
    private readonly string _dm;

    public ConversationServiceTests()
    {
        _sut = new ConversationService(new InMemoryConversationRepository(), _time);
        _dm = _sut.OpenDirect(Thandi, Sipho).Conversation!.ConversationId;
    }

    private static string Id(int n) => $"6f1c1a52-0000-4000-8000-{n:D12}";

    [Fact]
    public void OpenDirectIsIdempotentAndOrderInsensitive()
    {
        var again = _sut.OpenDirect(Sipho, Thandi);

        Assert.Equal(OpenOutcome.AlreadyExisted, again.Outcome);
        Assert.Equal(_dm, again.Conversation!.ConversationId);
        Assert.Single(_sut.ListFor(Thandi));
    }

    [Fact]
    public void CannotOpenAConversationWithYourself()
    {
        Assert.Equal(OpenOutcome.InvalidMembers, _sut.OpenDirect(Thandi, Thandi).Outcome);
    }

    [Fact]
    public void AcceptAssignsIncreasingSequenceNumbersInSendOrder()
    {
        var seqs = Enumerable.Range(1, 5)
            .Select(i => _sut.Accept(i % 2 == 0 ? Sipho : Thandi, _dm, Id(i)).Message!.Seq)
            .ToArray();

        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, seqs);
        Assert.Equal(5, _sut.Find(_dm)!.LastSeq);
    }

    [Fact]
    public void RetryWithSameMessageIdKeepsExactlyOneMessage()
    {
        var first = _sut.Accept(Thandi, _dm, Id(1));
        _time.Advance(TimeSpan.FromSeconds(30));
        var retry = _sut.Accept(Thandi, _dm, Id(1));

        Assert.Equal(AcceptOutcome.Accepted, first.Outcome);
        Assert.Equal(AcceptOutcome.Duplicate, retry.Outcome);
        Assert.Equal(first.Message, retry.Message);
        Assert.Single(_sut.ListReceipts(Thandi, _dm, 0, 100)!);
    }

    [Fact]
    public void MessageIdIsCaseInsensitive()
    {
        _sut.Accept(Thandi, _dm, "6F1C1A52-0000-4000-8000-0000000000AA");

        Assert.Equal(AcceptOutcome.Duplicate, _sut.Accept(Thandi, _dm, "6f1c1a52-0000-4000-8000-0000000000aa").Outcome);
    }

    [Fact]
    public void ReusingAnotherSendersMessageIdIsAConflict()
    {
        _sut.Accept(Thandi, _dm, Id(1));

        var result = _sut.Accept(Sipho, _dm, Id(1));

        Assert.Equal(AcceptOutcome.MessageIdConflict, result.Outcome);
        Assert.Null(result.Message);
        Assert.Single(_sut.ListReceipts(Thandi, _dm, 0, 100)!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("6f1c1a52000040008000000000000001")]
    [InlineData("{6f1c1a52-0000-4000-8000-000000000001}")]
    public void RejectsMessageIdsThatAreNotUuids(string id)
    {
        Assert.Equal(AcceptOutcome.InvalidMessageId, _sut.Accept(Thandi, _dm, id).Outcome);
    }

    [Fact]
    public void NonMemberCannotSend()
    {
        Assert.Equal(AcceptOutcome.NotAMember, _sut.Accept(Lerato, _dm, Id(1)).Outcome);
        Assert.Equal(AcceptOutcome.UnknownConversation, _sut.Accept(Thandi, "dm.nobody.nowhere", Id(2)).Outcome);
    }

    [Fact]
    public void AcceptNamesTheOtherMembersAsRecipients()
    {
        var result = _sut.Accept(Thandi, _dm, Id(1));

        Assert.Equal(new[] { Sipho }, result.RecipientIds);
    }

    [Fact]
    public void DeliveredAckFromRecipientTellsTheSender()
    {
        _sut.Accept(Thandi, _dm, Id(1));
        _time.Advance(TimeSpan.FromMilliseconds(400));

        var change = Assert.Single(_sut.MarkDelivered(Sipho, new[] { Id(1) }));

        Assert.Equal(Thandi, change.SenderId);
        Assert.Equal(TickStatus.Delivered, change.Status);
        Assert.Equal(_time.GetUtcNow(), change.At);
    }

    [Fact]
    public void DeliveredAckIgnoresUnknownIdsAndOwnMessages()
    {
        _sut.Accept(Thandi, _dm, Id(1));

        Assert.Empty(_sut.MarkDelivered(Thandi, new[] { Id(1) }));
        Assert.Empty(_sut.MarkDelivered(Sipho, new[] { Id(99), "not-a-uuid" }));
        Assert.Empty(_sut.MarkDelivered(Lerato, new[] { Id(1) }));
    }

    [Fact]
    public void OpeningTheChatMarksPartnersMessagesReadUpToTheSequence()
    {
        _sut.Accept(Thandi, _dm, Id(1)); // seq 1
        _sut.Accept(Sipho, _dm, Id(2));  // seq 2 (Sipho's own)
        _sut.Accept(Thandi, _dm, Id(3)); // seq 3
        _sut.Accept(Thandi, _dm, Id(4)); // seq 4, not yet on screen
        _sut.MarkDelivered(Sipho, new[] { Id(1), Id(3), Id(4) });

        var changes = _sut.MarkReadUpTo(Sipho, _dm, 3);

        Assert.Equal(new[] { Id(1), Id(3) }, changes.Select(c => c.MessageId).ToArray());
        Assert.All(changes, c => Assert.Equal(TickStatus.Read, c.Status));
        var receipts = _sut.ListReceipts(Thandi, _dm, 0, 100)!;
        Assert.Equal(
            new[] { TickStatus.Read, TickStatus.Sent, TickStatus.Read, TickStatus.Delivered },
            receipts.Select(r => r.Status).ToArray());
    }

    [Fact]
    public void ReadingTwiceProducesNoNewTicks()
    {
        _sut.Accept(Thandi, _dm, Id(1));
        _sut.MarkReadUpTo(Sipho, _dm, 1);

        Assert.Empty(_sut.MarkReadUpTo(Sipho, _dm, 1));
        Assert.Empty(_sut.MarkReadUpTo(Lerato, _dm, 1));
    }

    [Fact]
    public void ReceiptsArePagedBySequenceAndHiddenFromNonMembers()
    {
        for (var i = 1; i <= 5; i++)
        {
            _sut.Accept(Thandi, _dm, Id(i));
        }

        var page = _sut.ListReceipts(Sipho, _dm, afterSeq: 2, limit: 2)!;

        Assert.Equal(new long[] { 3, 4 }, page.Select(r => r.Seq).ToArray());
        Assert.Null(_sut.ListReceipts(Lerato, _dm, 0, 10));
        Assert.Null(_sut.ListReceipts(Thandi, "dm.unknown.x", 0, 10));
    }

    [Fact]
    public void ReceiptsCarryNoMessageContent()
    {
        var props = typeof(MessageReceipt).GetProperties().Select(p => p.Name.ToUpperInvariant()).ToList();

        Assert.DoesNotContain(props, p => p.Contains("BODY", StringComparison.Ordinal) || p.Contains("CONTENT", StringComparison.Ordinal) || p.Contains("TEXT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConcurrentSendsGetUniqueGaplessSequenceNumbers()
    {
        var tasks = Enumerable.Range(1, 200)
            .Select(i => Task.Run(() => _sut.Accept(i % 2 == 0 ? Sipho : Thandi, _dm, Id(i)).Message!.Seq))
            .ToArray();

        var seqs = await Task.WhenAll(tasks);

        Assert.Equal(Enumerable.Range(1, 200).Select(i => (long)i).ToArray(), seqs.OrderBy(s => s).ToArray());
    }
}
