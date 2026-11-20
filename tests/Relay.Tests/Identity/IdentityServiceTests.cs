using Relay.Identity.Application;
using Relay.Identity.Contracts;
using Xunit;

namespace Relay.Identity.Tests;

public class IdentityServiceTests
{
    private readonly IdentityService _sut = new();

    public IdentityServiceTests() => _sut.RegisterUser("u_thandi", "Thandi Mokoena");

    [Fact]
    public void RegisteringGivesADefaultPhoneDeviceAndInitials()
    {
        var user = _sut.FindUser("u_thandi")!;

        Assert.Equal("TM", user.Initials);
        Assert.Equal(new[] { "thandi-phone" }, _sut.DevicesOf("u_thandi").ToArray());
    }

    [Fact]
    public void DemoSessionTokenResolvesToUserAndDevice()
    {
        var session = _sut.StartDemoSession("u_thandi", null).Session!;

        Assert.Equal(new SessionPrincipal("u_thandi", "thandi-phone"), _sut.Resolve(session.Token));
        Assert.Null(_sut.Resolve("made-up-token"));
        Assert.Null(_sut.Resolve(null));
    }

    [Fact]
    public void EachSessionGetsANewToken()
    {
        Assert.False(_sut.StartDemoSession("u_thandi", null).Session!.Token == _sut.StartDemoSession("u_thandi", null).Session!.Token);
    }

    [Fact]
    public void ANewDeviceIsRemembered()
    {
        _sut.StartDemoSession("u_thandi", "thandi-tablet");

        Assert.Equal(new[] { "thandi-phone", "thandi-tablet" }, _sut.DevicesOf("u_thandi").ToArray());
    }

    [Theory]
    [InlineData("u_nobody", null, SessionOutcome.UnknownUser)]
    [InlineData("u_thandi", "Bad Device!", SessionOutcome.InvalidDevice)]
    [InlineData("u_thandi", "ab", SessionOutcome.InvalidDevice)]
    [InlineData("u_thandi", "web-7f3a", SessionOutcome.Started)]
    public void SessionRules(string userId, string? deviceId, SessionOutcome expected)
    {
        Assert.Equal(expected, _sut.StartDemoSession(userId, deviceId).Outcome);
    }

    [Fact]
    public void InvalidUserIdsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => _sut.RegisterUser("Thandi", "Thandi"));
    }
}
