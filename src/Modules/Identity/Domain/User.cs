using System.Text.RegularExpressions;

namespace Relay.Identity.Domain;

internal sealed partial class User
{
    private readonly HashSet<string> _devices = new(StringComparer.Ordinal);

    public User(string id, string displayName)
    {
        if (!IsValidUserId(id))
        {
            throw new ArgumentException("User IDs look like u_name.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A display name is required.", nameof(displayName));
        }

        Id = id;
        DisplayName = displayName.Trim();
        _devices.Add(DefaultDevice);
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string DefaultDevice => $"{Id[2..]}-phone";

    public IReadOnlyCollection<string> Devices => _devices;

    public string Initials =>
        string.Concat(DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    public static bool IsValidUserId(string? id) => id is not null && UserIdPattern().IsMatch(id);

    public static bool IsValidDeviceId(string? id) => id is not null && DeviceIdPattern().IsMatch(id);

    public void AddDevice(string deviceId) => _devices.Add(deviceId);

    [GeneratedRegex("^u_[a-z0-9]{2,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex UserIdPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{2,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex DeviceIdPattern();
}
