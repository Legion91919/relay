using Relay.Presence.Application;
using Relay.Support.Tests;
using Xunit;

namespace Relay.Presence.Tests;

public class PresenceServiceTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly PresenceService _sut;

    public PresenceServiceTests() => _sut = new PresenceService(_time);

    [Fact]
    public void FirstConnectionBringsUserOnline()
    {
        var change = _sut.Connected("u_sipho", "sipho-phone", "c1");

        Assert.True(change!.Online);
        Assert.Null(_sut.Connected("u_sipho", "sipho-tablet", "c2"));
        Assert.Equal(new[] { "sipho-phone", "sipho-tablet" }, _sut.OnlineDevices("u_sipho").Order().ToArray());
    }

    [Fact]
    public void UserGoesOfflineOnlyWhenTheLastConnectionCloses()
    {
        _sut.Connected("u_sipho", "sipho-phone", "c1");
        _sut.Connected("u_sipho", "sipho-tablet", "c2");
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Null(_sut.Disconnected("c1"));
        var offline = _sut.Disconnected("c2");

        Assert.False(offline!.Online);
        Assert.Equal(_time.GetUtcNow(), offline.LastSeenAt);
        Assert.Equal(offline, _sut.StatusOf("u_sipho"));
    }

    [Fact]
    public void UnknownConnectionsAndUsersAreHarmless()
    {
        Assert.Null(_sut.Disconnected("nope"));
        Assert.False(_sut.StatusOf("u_ghost").Online);
        Assert.Empty(_sut.OnlineDevices("u_ghost"));
    }
}
