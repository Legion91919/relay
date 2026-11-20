using Relay.Delivery.Contracts;
using Relay.Delivery.Domain;
using Xunit;

namespace Relay.Delivery.Tests;

public class DeviceQueueTests
{
    private static string[] Ids(DeviceQueue q) => q.Entries.Select(e => e.MessageId).ToArray();

    [Fact]
    public void KeepsArrivalOrder()
    {
        var q = new DeviceQueue("sipho-phone");
        q.Add(new QueueEntry("a", "c1", 1));
        q.Add(new QueueEntry("b", "c1", 2));
        q.Add(new QueueEntry("c", "c1", 3));

        Assert.Equal(new[] { "a", "b", "c" }, Ids(q));
    }

    [Fact]
    public void LateArrivalSlotsInBySequenceWithinItsConversation()
    {
        var q = new DeviceQueue("sipho-phone");
        q.Add(new QueueEntry("m1", "c1", 1));
        q.Add(new QueueEntry("x1", "c2", 1));
        q.Add(new QueueEntry("m3", "c1", 3));
        q.Add(new QueueEntry("m2", "c1", 2));

        Assert.Equal(new[] { "m1", "x1", "m2", "m3" }, Ids(q));
    }

    [Fact]
    public void AddingTheSameMessageTwiceKeepsOneEntry()
    {
        var q = new DeviceQueue("sipho-phone");

        Assert.True(q.Add(new QueueEntry("a", "c1", 1)));
        Assert.False(q.Add(new QueueEntry("a", "c1", 1)));
        Assert.Single(q.Entries);
    }

    [Fact]
    public void RemoveTakesOnlyThatMessage()
    {
        var q = new DeviceQueue("sipho-phone");
        q.Add(new QueueEntry("a", "c1", 1));
        q.Add(new QueueEntry("b", "c1", 2));

        Assert.True(q.Remove("a"));
        Assert.False(q.Remove("a"));
        Assert.Equal(new[] { "b" }, Ids(q));
    }

    [Theory]
    [InlineData(null, BodyProblem.Required)]
    [InlineData("", BodyProblem.Required)]
    [InlineData("   \n ", BodyProblem.Required)]
    [InlineData("Sawubona", BodyProblem.None)]
    public void BodyRules(string? body, BodyProblem expected)
    {
        Assert.Equal(expected, MessageBody.Validate(body, 10));
    }

    [Fact]
    public void BodyLongerThanTheLimitIsRejected()
    {
        Assert.Equal(BodyProblem.TooLong, MessageBody.Validate(new string('a', 11), 10));
        Assert.Equal(BodyProblem.None, MessageBody.Validate(new string('a', 10), 10));
    }
}
