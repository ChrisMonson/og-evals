using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>A slain: condition holds once that creature has been killed.</summary>
public class SlainConditionTests
{
    private const string Json = """
    {
      "spawnLocation": 1, "exitNeedsKey": false,
      "monsters": [
        { "name": "wolf", "description": "A grey wolf.", "hitPoints": 1, "armorClass": 1,
          "attackStrength": 0, "attackDice": 1 }
      ],
      "locations": [
        { "index": 1, "name": "Den", "description": "A den.", "monster": "wolf",
          "interactions": [
            { "interactionId": "on", "triggers": ["north", "go north"], "if": ["slain:wolf"],
              "actions": [ { "action": "changelocation", "parameters": ["2"] } ] },
            { "interactionId": "blocked", "triggers": ["north", "go north"], "ifNot": ["slain:wolf"],
              "actions": [ { "action": "describe", "parameters": ["The wolf fills the tunnel."] } ] }
          ] },
        { "index": 2, "name": "Daylight", "description": "Daylight.", "isExit": true }
      ]
    }
    """;

    [Fact]
    public void ACreatureClosesThePassageUntilSlain()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "north");
        Assert.Contains("fills the tunnel", output.Text);
        Assert.Equal(1, session.CurrentLocationId);

        engine.HandleInput(session, "attack wolf");
        engine.HandleInput(session, "north");
        Assert.Equal(2, session.CurrentLocationId);
    }

    [Fact]
    public void TheValidatorCatchesASlainConditionForACreatureThatDoesNotExist()
    {
        var world = World.FromJson(Json.Replace("\"if\": [\"slain:wolf\"]", "\"if\": [\"slain:dragon\"]"));

        Assert.Contains(WorldValidator.Validate(world), issue => issue.Message.Contains("slain:dragon"));
    }

    [Fact]
    public void TheFixtureIsValid() => Assert.True(WorldFixture.IsValid(Json));
}
