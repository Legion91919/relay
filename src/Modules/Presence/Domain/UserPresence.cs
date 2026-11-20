namespace Relay.Presence.Domain;

/// <summary>A user is online while at least one connection (any device) is open.</summary>
internal sealed class UserPresence(string userId)
{
    private readonly Dictionary<string, string> _deviceByConnection = new(StringComparer.Ordinal);

    public string UserId { get; } = userId;

    public bool Online => _deviceByConnection.Count > 0;

    public DateTimeOffset? LastSeenAt { get; private set; }

    public IReadOnlyCollection<string> Devices => _deviceByConnection.Values.Distinct(StringComparer.Ordinal).ToList();

    /// <summary>Returns true if this connection brought the user online.</summary>
    public bool Connect(string connectionId, string deviceId, DateTimeOffset at)
    {
        var wasOnline = Online;
        _deviceByConnection[connectionId] = deviceId;
        LastSeenAt = at;
        return !wasOnline;
    }

    /// <summary>Returns true if closing this connection took the user offline.</summary>
    public bool Disconnect(string connectionId, DateTimeOffset at)
    {
        if (!_deviceByConnection.Remove(connectionId))
        {
            return false;
        }

        LastSeenAt = at;
        return !Online;
    }
}
