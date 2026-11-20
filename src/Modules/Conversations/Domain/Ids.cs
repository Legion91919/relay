namespace Relay.Conversations.Domain;

internal static class ConversationIds
{
    /// <summary>One-to-one conversations have a stable ID made from both members, so opening twice finds the same one.</summary>
    public static string Direct(string userA, string userB) =>
        string.CompareOrdinal(userA, userB) < 0 ? $"dm.{userA}.{userB}" : $"dm.{userB}.{userA}";
}

internal static class MessageIds
{
    /// <summary>Client-generated message IDs must be UUIDs in the standard 36-character form. Returns the lower-case form.</summary>
    public static bool TryNormalise(string? raw, out string normalised)
    {
        normalised = string.Empty;
        if (raw is null || raw.Length != 36 || !Guid.TryParseExact(raw, "D", out var guid))
        {
            return false;
        }

        normalised = guid.ToString("D");
        return true;
    }
}
