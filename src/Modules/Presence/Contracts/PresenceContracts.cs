namespace Relay.Presence.Contracts;

public sealed record PresenceStatus(string UserId, bool Online, DateTimeOffset? LastSeenAt);

/// <summary>Public entry point of the Presence module: which users have a live connection right now.</summary>
public interface IPresence
{
    /// <summary>Records a live connection. Returns the new status only if the user just came online.</summary>
    PresenceStatus? Connected(string userId, string deviceId, string connectionId);

    /// <summary>Removes a connection. Returns the new status only if the user just went offline.</summary>
    PresenceStatus? Disconnected(string connectionId);

    PresenceStatus StatusOf(string userId);

    IReadOnlyCollection<string> OnlineDevices(string userId);
}
