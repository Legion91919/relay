namespace Relay.Support.Tests;

/// <summary>A clock the test moves by hand (the testing package is not available offline).</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public ManualTimeProvider()
        : this(new DateTimeOffset(2026, 11, 19, 8, 0, 0, TimeSpan.Zero))
    {
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
