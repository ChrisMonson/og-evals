using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>A creature answers to generic words for what it is, as well as its name.</summary>
public class AnyCreatureTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "monsters": [ { "name": "wolf", "description": "A wolf.", "hitPoints": 99, "armorClass": 1, "attackStrength": 0, "attackDice": 1 } ],
      "locations": [ { "index": 1, "name": "Hall", "description": "A hall.", "monster": "wolf" } ]
    }
    """;

    [Theory]
    [InlineData("attack monster")]
    [InlineData("attack the creature")]
    [InlineData("attack it")]
    [InlineData("kill the thing")]
    public void ItAnswersToWhatItIs(string command)
    {
        var (engine, events, session, _) = WorldFixture.Start(Json, seed: 1);

        engine.HandleInput(session, command);

        Assert.Contains(events.Events, e => e.Type == EventType.Combat && e.Payload.GetValueOrDefault("target") == "wolf");
    }

    [Fact]
    public void ADifferentNameIsStillNotIt()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json, seed: 1);

        engine.HandleInput(session, "attack the dragon");

        Assert.DoesNotContain(events.Events, e => e.Type == EventType.Combat);
        Assert.Contains("no dragon here", output.Text);
    }
}
