using Relay.Presence.Contracts;
using Relay.Presence.Domain;

namespace Relay.Presence.Application;

/// <summary>In-process connection registry. At scale this moves to the connection gateway with a shared store (see ADR-0001).</summary>
internal sealed class PresenceService(TimeProvider time) : IPresence
{
    private readonly object _gate = new();
    private readonly Dictionary<string, UserPresence> _users = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _userByConnection = new(StringComparer.Ordinal);

    public PresenceStatus? Connected(string userId, string deviceId, string connectionId)
    {
        lock (_gate)
        {
            var presence = GetOrAdd(userId);
            _userByConnection[connectionId] = userId;
            return presence.Connect(connectionId, deviceId, time.GetUtcNow()) ? ToStatus(presence) : null;
        }
    }

    public PresenceStatus? Disconnected(string connectionId)
    {
        lock (_gate)
        {
            if (!_userByConnection.Remove(connectionId, out var userId))
            {
                return null;
            }

            var presence = _users[userId];
            return presence.Disconnect(connectionId, time.GetUtcNow()) ? ToStatus(presence) : null;
        }
    }

    public PresenceStatus StatusOf(string userId)
    {
        lock (_gate)
        {
            return _users.TryGetValue(userId, out var p) ? ToStatus(p) : new PresenceStatus(userId, false, null);
        }
    }

    public IReadOnlyCollection<string> OnlineDevices(string userId)
    {
        lock (_gate)
        {
            return _users.TryGetValue(userId, out var p) ? p.Devices : [];
        }
    }

    private UserPresence GetOrAdd(string userId)
    {
        if (!_users.TryGetValue(userId, out var p))
        {
            p = new UserPresence(userId);
            _users[userId] = p;
        }

        return p;
    }

    private static PresenceStatus ToStatus(UserPresence p) => new(p.UserId, p.Online, p.Online ? null : p.LastSeenAt);
}
