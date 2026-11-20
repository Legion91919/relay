using Relay.Conversations.Contracts;

namespace Relay.Conversations.Domain;

/// <summary>A conversation owns the order of its messages: every accepted message gets the next sequence number.</summary>
internal sealed class Conversation
{
    private readonly List<ChatMessage> _messages = [];
    private readonly string[] _members;

    private Conversation(string id, string[] members, DateTimeOffset createdAt)
    {
        Id = id;
        _members = members;
        CreatedAt = createdAt;
    }

    public string Id { get; }

    public IReadOnlyList<string> Members => _members;

    public DateTimeOffset CreatedAt { get; }

    public long LastSeq { get; private set; }

    public DateTimeOffset? LastMessageAt { get; private set; }

    /// <summary>Messages in sequence order.</summary>
    public IReadOnlyList<ChatMessage> Messages => _messages;

    public static Conversation Direct(string userA, string userB, DateTimeOffset createdAt)
    {
        if (string.Equals(userA, userB, StringComparison.Ordinal))
        {
            throw new ArgumentException("A direct conversation needs two different people.", nameof(userB));
        }

        var members = new[] { userA, userB };
        Array.Sort(members, StringComparer.Ordinal);
        return new Conversation(ConversationIds.Direct(userA, userB), members, createdAt);
    }

    public bool IsMember(string userId) => Array.Exists(_members, m => string.Equals(m, userId, StringComparison.Ordinal));

    public ChatMessage Append(string messageId, string senderId, DateTimeOffset sentAt)
    {
        if (!IsMember(senderId))
        {
            throw new InvalidOperationException("Only members can send to a conversation.");
        }

        LastSeq++;
        LastMessageAt = sentAt;
        var message = new ChatMessage(messageId, Id, LastSeq, senderId, sentAt, _members.Where(m => !string.Equals(m, senderId, StringComparison.Ordinal)));
        _messages.Add(message);
        return message;
    }

    /// <summary>The reader has seen everything up to <paramref name="upToSeq"/>; ticks the other members' messages as read.</summary>
    public IReadOnlyList<TickChange> MarkReadUpTo(string readerId, long upToSeq, DateTimeOffset at)
    {
        if (!IsMember(readerId))
        {
            return [];
        }

        var changes = new List<TickChange>();
        foreach (var message in _messages)
        {
            if (message.Seq > upToSeq)
            {
                break;
            }

            if (message.MarkRead(readerId, at) is { } change)
            {
                changes.Add(change);
            }
        }

        return changes;
    }

    public ConversationSummary ToSummary() => new(Id, _members, CreatedAt, LastSeq, LastMessageAt);
}
