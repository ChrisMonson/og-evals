using Og.Core.Runtime;

namespace Og.Core.Tests;

public class CombinedTriggerTests
{
    [Theory]
    [InlineData("give*&*bread*", "give the fresh bread to the guard", true)]
    [InlineData("give*&*bread*", "give the sword to the guard", false)]
    [InlineData("give*&*bread*", "ask the guard to let me pass for the bread", false)]
    [InlineData("*ask*&*ring*&*guard*", "ask the guard about the ring", true)]
    [InlineData("*ask*&*ring*&*guard*", "ask the old man about the ring", false)]
    [InlineData("&", "anything", false)]
    public void EveryPartMustMatch(string trigger, string command, bool expected) =>
        Assert.Equal(expected, TriggerMatcher.Matches(trigger, command));
}
