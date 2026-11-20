using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Relay.Conversations.Application;
using Relay.Conversations.Contracts;
using Relay.Conversations.Infrastructure;

namespace Relay.Conversations;

public static class ConversationsModule
{
    public static IServiceCollection AddConversationsModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IConversationRepository, InMemoryConversationRepository>();
        services.AddSingleton<IConversations, ConversationService>();
        return services;
    }
}
