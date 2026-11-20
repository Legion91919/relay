using Relay.Conversations.Contracts;
using Relay.Conversations.Domain;

namespace Relay.Conversations.Application;

/// <summary>
/// Use cases of the Conversations module. One lock keeps sequence numbers gapless and ticks consistent in memory;
/// slice 2 replaces it with a database transaction per conversation.
/// </summary>
internal sealed class ConversationService(IConversationRepository repository, TimeProvider time) : IConversations
{
    public const int MaxPageSize = 500;

    private readonly object _gate = new();

    public OpenResult OpenDirect(string userId, string partnerId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(partnerId) || string.Equals(userId, partnerId, StringComparison.Ordinal))
        {
            return new OpenResult(OpenOutcome.InvalidMembers, null);
        }

        lock (_gate)
        {
            var id = ConversationIds.Direct(userId, partnerId);
            if (repository.Get(id) is { } existing)
            {
                return new OpenResult(OpenOutcome.AlreadyExisted, existing.ToSummary());
            }

            var created = Conversation.Direct(userId, partnerId, time.GetUtcNow());
            repository.Add(created);
            return new OpenResult(OpenOutcome.Opened, created.ToSummary());
        }
    }

    public ConversationSummary? Find(string conversationId)
    {
        lock (_gate)
        {
            return repository.Get(conversationId)?.ToSummary();
        }
    }

    public IReadOnlyList<ConversationSummary> ListFor(string userId)
    {
        lock (_gate)
        {
            return repository.ForMember(userId)
                .Select(c => c.ToSummary())
                .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
                .ToList();
        }
    }

    public AcceptResult Accept(string senderId, string conversationId, string messageId)
    {
        if (!MessageIds.TryNormalise(messageId, out var id))
        {
            return new AcceptResult(AcceptOutcome.InvalidMessageId, null, []);
        }

        lock (_gate)
        {
            if (repository.Get(conversationId) is not { } conversation)
            {
                return new AcceptResult(AcceptOutcome.UnknownConversation, null, []);
            }

            if (!conversation.IsMember(senderId))
            {
                return new AcceptResult(AcceptOutcome.NotAMember, null, []);
            }

            var recipients = conversation.Members.Where(m => !string.Equals(m, senderId, StringComparison.Ordinal)).ToList();
            if (repository.FindMessage(id) is { } existing)
            {
                var sameSend = string.Equals(existing.SenderId, senderId, StringComparison.Ordinal)
                    && string.Equals(existing.ConversationId, conversationId, StringComparison.Ordinal);
                return sameSend
                    ? new AcceptResult(AcceptOutcome.Duplicate, existing.ToReceipt(), recipients)
                    : new AcceptResult(AcceptOutcome.MessageIdConflict, null, []);
            }

            var message = conversation.Append(id, senderId, time.GetUtcNow());
            repository.IndexMessage(message);
            return new AcceptResult(AcceptOutcome.Accepted, message.ToReceipt(), recipients);
        }
    }

    public IReadOnlyList<TickChange> MarkDelivered(string recipientId, IEnumerable<string> messageIds)
    {
        var changes = new List<TickChange>();
        lock (_gate)
        {
            var now = time.GetUtcNow();
            foreach (var raw in messageIds)
            {
                if (MessageIds.TryNormalise(raw, out var id)
                    && repository.FindMessage(id) is { } message
                    && message.MarkDelivered(recipientId, now) is { } change)
                {
                    changes.Add(change);
                }
            }
        }

        return changes;
    }

    public IReadOnlyList<TickChange> MarkReadUpTo(string readerId, string conversationId, long upToSeq)
    {
        lock (_gate)
        {
            return repository.Get(conversationId) is { } conversation
                ? conversation.MarkReadUpTo(readerId, upToSeq, time.GetUtcNow())
                : [];
        }
    }

    public IReadOnlyList<MessageReceipt>? ListReceipts(string userId, string conversationId, long afterSeq, int limit)
    {
        lock (_gate)
        {
            if (repository.Get(conversationId) is not { } conversation || !conversation.IsMember(userId))
            {
                return null;
            }

            return conversation.Messages
                .Where(m => m.Seq > afterSeq)
                .Take(Math.Clamp(limit, 1, MaxPageSize))
                .Select(m => m.ToReceipt())
                .ToList();
        }
    }
}
