namespace Relay.Identity.Contracts;

public sealed record UserSummary(string UserId, string DisplayName, string Initials);

/// <summary>A signed-in device. The raw token is returned once and never stored (only its hash is).</summary>
public sealed record DemoSession(string Token, string UserId, string DeviceId, string DisplayName);

public sealed record SessionPrincipal(string UserId, string DeviceId);

public enum SessionOutcome
{
    Started,
    UnknownUser,
    InvalidDevice,
}

public sealed record SessionResult(SessionOutcome Outcome, DemoSession? Session);

/// <summary>Public entry point of the Identity module: people, their devices and demo sign-in.</summary>
public interface IIdentity
{
    IReadOnlyList<UserSummary> ListUsers();

    UserSummary? FindUser(string userId);

    /// <summary>Adds a user with a default device "&lt;name&gt;-phone". Idempotent.</summary>
    UserSummary RegisterUser(string userId, string displayName);

    /// <summary>Development sign-in: no password, a random bearer token bound to one device.</summary>
    SessionResult StartDemoSession(string userId, string? deviceId);

    SessionPrincipal? Resolve(string? token);

    IReadOnlyList<string> DevicesOf(string userId);
}
