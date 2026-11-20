using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Relay.Identity.Contracts;

namespace Relay.Api.Auth;

/// <summary>
/// Bearer tokens issued by <c>POST /api/identity/demo-sessions</c>. WebSockets cannot send headers from a browser,
/// so on <c>/hubs</c> the token may also come as the <c>access_token</c> query value (the SignalR convention).
/// </summary>
internal sealed class DemoTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IIdentity identity)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DemoToken";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? token = null;
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = header["Bearer ".Length..].Trim();
        }
        else if (Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            token = Request.Query["access_token"].ToString();
        }

        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (identity.Resolve(token) is not { } principal)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown or expired token."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, principal.UserId),
            new Claim(PrincipalExtensions.DeviceClaim, principal.DeviceId),
        };
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

internal static class PrincipalExtensions
{
    public const string DeviceClaim = "relay:device";

    public static SessionPrincipal ToPrincipal(this ClaimsPrincipal user) => new(
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Not signed in."),
        user.FindFirstValue(DeviceClaim) ?? throw new InvalidOperationException("No device on the session."));
}
