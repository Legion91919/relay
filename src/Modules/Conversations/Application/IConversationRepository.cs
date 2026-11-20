using Relay.Conversations.Domain;

namespace Relay.Conversations.Application;

/// <summary>Port for conversation storage. In memory for slice 1; Postgres in slice 2.</summary>
internal interface IConversationRepository
{
    Conversation? Get(string conversationId);

    void Add(Conversation conversation);

    IReadOnlyList<Conversation> ForMember(string userId);

    ChatMessage? FindMessage(string messageId);

    void IndexMessage(ChatMessage message);
}
