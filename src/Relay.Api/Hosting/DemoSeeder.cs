using Relay.Conversations.Contracts;
using Relay.Identity.Contracts;

namespace Relay.Api.Hosting;

/// <summary>Invented demo people and chats for Development. No message bodies are seeded: content lives on phones.</summary>
public static class DemoSeeder
{
    public static readonly IReadOnlyList<(string Id, string Name)> Users =
    [
        ("u_thandi", "Thandi Mokoena"),
        ("u_sipho", "Sipho Dlamini"),
        ("u_lerato", "Lerato Nkosi"),
        ("u_pule", "Pule Molefe"),
    ];

    public static void Seed(IIdentity identity, IConversations conversations)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(conversations);
        foreach (var (id, name) in Users)
        {
            identity.RegisterUser(id, name);
        }

        conversations.OpenDirect("u_thandi", "u_sipho");
        conversations.OpenDirect("u_thandi", "u_lerato");
        conversations.OpenDirect("u_sipho", "u_lerato");
        conversations.OpenDirect("u_thandi", "u_pule");
    }
}
