using System.Collections.Concurrent;
using Relay.Conversations.Application;
using Relay.Conversations.Domain;

namespace Relay.Conversations.Infrastructure;

/// <summary>Slice-1 storage. Thread safety for multi-step changes comes from the service's lock; the maps are concurrent for safe reads.</summary>
internal sealed class InMemoryConversationRepository : IConversationRepository
{
    private readonly ConcurrentDictionary<string, Conversation> _conversations = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ChatMessage> _messages = new(StringComparer.Ordinal);

    public Conversation? Get(string conversationId) => _conversations.GetValueOrDefault(conversationId);

    public void Add(Conversation conversation) => _conversations.TryAdd(conversation.Id, conversation);

    public IReadOnlyList<Conversation> ForMember(string userId) =>
        _conversations.Values.Where(c => c.IsMember(userId)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();

    public ChatMessage? FindMessage(string messageId) => _messages.GetValueOrDefault(messageId);

    public void IndexMessage(ChatMessage message) => _messages.TryAdd(message.Id, message);
}
