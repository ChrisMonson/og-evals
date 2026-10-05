using Og.Core.Runtime;

namespace Og.Core.Tests;

public class WordGroupTests
{
    private static readonly Dictionary<string, string[]> Words = new()
    {
        ["give"] = ["give", "hand"],
        ["guard"] = ["guard", "sentry"],
    };

    [Fact]
    public void AGroupStandsForEachOfItsWords() =>
        Assert.Equal(["give *&*bread*", "hand *&*bread*"], WordGroups.Expand("{give} *&*bread*", Words));

    [Fact]
    public void SeveralGroupsExpandToEveryCombination() =>
        Assert.Equal(["give*&*guard*", "give*&*sentry*", "hand*&*guard*", "hand*&*sentry*"],
            WordGroups.Expand("{give}*&*{guard}*", Words));

    [Fact]
    public void ATriggerWithoutBracesIsUnchanged() =>
        Assert.Equal(["*bread*"], WordGroups.Expand("*bread*", Words));

    private const string Json = """
    {
      "name": "words", "spawnLocation": 1,
      "words": { "give": ["give", "hand"], "guard": ["guard", "sentry"] },
      "items": [ { "name": "bread", "description": "A loaf." } ],
      "locations": [
        { "index": 1, "name": "Gate", "description": "A gate.", "ground": ["bread"],
          "interactions": [
            { "interactionId": "fed", "triggers": ["{give} *&*{guard}*"],
              "actions": [ { "action": "describe", "parameters": ["The guard eats."] } ] }
          ] }
      ]
    }
    """;

    [Theory]
    [InlineData("give the bread to the guard")]
    [InlineData("hand the sentry the bread")]
    public void AWorldsTriggersUseItsGroups(string command)
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, command);

        Assert.Contains("The guard eats.", output.Text);
    }

    [Fact]
    public void TheValidatorReportsAGroupThatDoesNotExist()
    {
        Assert.True(WorldFixture.IsValid(Json));
        Assert.False(WorldFixture.IsValid(Json.Replace("{guard}*\"]", "{watchman}*\"]")));
    }
}
