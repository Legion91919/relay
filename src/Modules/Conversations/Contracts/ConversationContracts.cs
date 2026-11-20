namespace Relay.Conversations.Contracts;

/// <summary>Tick state of a message. Only ever moves forward: Sent, then Delivered, then Read.</summary>
public enum TickStatus
{
    Sent = 1,
    Delivered = 2,
    Read = 3,
}

/// <summary>A conversation as other modules and the API see it.</summary>
public sealed record ConversationSummary(
    string ConversationId,
    IReadOnlyList<string> MemberIds,
    DateTimeOffset CreatedAt,
    long LastSeq,
    DateTimeOffset? LastMessageAt);

/// <summary>Receipts metadata for one message. Deliberately has no body: content never lives in this module.</summary>
public sealed record MessageReceipt(
    string MessageId,
    string ConversationId,
    long Seq,
    string SenderId,
    DateTimeOffset SentAt,
    TickStatus Status);

public enum AcceptOutcome
{
    Accepted,
    Duplicate,
    InvalidMessageId,
    UnknownConversation,
    NotAMember,
    MessageIdConflict,
}

/// <summary>Result of accepting a message. <see cref="Message"/> is set for Accepted and Duplicate.</summary>
public sealed record AcceptResult(AcceptOutcome Outcome, MessageReceipt? Message, IReadOnlyList<string> RecipientIds);

public enum OpenOutcome
{
    Opened,
    AlreadyExisted,
    InvalidMembers,
}

public sealed record OpenResult(OpenOutcome Outcome, ConversationSummary? Conversation);

/// <summary>A tick moved forward. Sent to <see cref="SenderId"/> so their phone can update the ticks.</summary>
public sealed record TickChange(string ConversationId, string MessageId, string SenderId, TickStatus Status, DateTimeOffset At, DateTimeOffset SentAt);

/// <summary>Public entry point of the Conversations module.</summary>
public interface IConversations
{
    /// <summary>Gets or creates the one-to-one conversation between two users (order does not matter).</summary>
    OpenResult OpenDirect(string userId, string partnerId);

    ConversationSummary? Find(string conversationId);

    IReadOnlyList<ConversationSummary> ListFor(string userId);

    /// <summary>
    /// Accepts a message with a client-generated ID and gives it the next sequence number in the conversation.
    /// Retrying with the same ID returns the original message (Duplicate) and never creates a second one.
    /// </summary>
    AcceptResult Accept(string senderId, string conversationId, string messageId);

    /// <summary>The recipient's device has the messages. Returns the ticks that moved forward.</summary>
    IReadOnlyList<TickChange> MarkDelivered(string recipientId, IEnumerable<string> messageIds);

    /// <summary>The reader has seen every message up to <paramref name="upToSeq"/>. Returns the ticks that moved forward.</summary>
    IReadOnlyList<TickChange> MarkReadUpTo(string readerId, string conversationId, long upToSeq);

    /// <summary>Receipts after a sequence number, oldest first. Null when the conversation does not exist or the user is not a member.</summary>
    IReadOnlyList<MessageReceipt>? ListReceipts(string userId, string conversationId, long afterSeq, int limit);
}
