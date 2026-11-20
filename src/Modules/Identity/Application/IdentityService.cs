using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Relay.Identity.Contracts;
using Relay.Identity.Domain;

namespace Relay.Identity.Application;

/// <summary>Slice-1 identity: in-memory users and demo sessions. Real sign-in (SMS one-time PIN) is a later slice.</summary>
internal sealed class IdentityService : IIdentity
{
    private readonly ConcurrentDictionary<string, User> _users = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SessionPrincipal> _sessionsByTokenHash = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public IReadOnlyList<UserSummary> ListUsers() =>
        _users.Values.OrderBy(u => u.DisplayName, StringComparer.Ordinal).Select(ToSummary).ToList();

    public UserSummary? FindUser(string userId) => _users.TryGetValue(userId ?? string.Empty, out var u) ? ToSummary(u) : null;

    public UserSummary RegisterUser(string userId, string displayName) => ToSummary(_users.GetOrAdd(userId, id => new User(id, displayName)));

    public SessionResult StartDemoSession(string userId, string? deviceId)
    {
        if (!_users.TryGetValue(userId ?? string.Empty, out var user))
        {
            return new SessionResult(SessionOutcome.UnknownUser, null);
        }

        var device = string.IsNullOrWhiteSpace(deviceId) ? user.DefaultDevice : deviceId.Trim();
        if (!User.IsValidDeviceId(device))
        {
            return new SessionResult(SessionOutcome.InvalidDevice, null);
        }

        lock (_gate)
        {
            user.AddDevice(device);
        }

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        _sessionsByTokenHash[Hash(token)] = new SessionPrincipal(user.Id, device);
        return new SessionResult(SessionOutcome.Started, new DemoSession(token, user.Id, device, user.DisplayName));
    }

    public SessionPrincipal? Resolve(string? token) =>
        string.IsNullOrWhiteSpace(token) ? null : _sessionsByTokenHash.GetValueOrDefault(Hash(token));

    public IReadOnlyList<string> DevicesOf(string userId)
    {
        if (!_users.TryGetValue(userId ?? string.Empty, out var user))
        {
            return [];
        }

        lock (_gate)
        {
            return user.Devices.Order(StringComparer.Ordinal).ToList();
        }
    }

    private static UserSummary ToSummary(User u) => new(u.Id, u.DisplayName, u.Initials);

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
