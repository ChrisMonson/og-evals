using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>A monster that blocks a direction: the exit still shows, but the way is refused while it lives.</summary>
public class BlockingMonsterTests
{
    private const string Json = """
    {
      "spawnLocation": 1, "exitNeedsKey": false,
      "darkness": { "blocked": "Something huge is in the way.", "fallChance": 0 },
      "items": [ { "name": "lamp", "description": "A lamp.", "isLight": true } ],
      "monsters": [ { "name": "wolf", "description": "A wolf.", "hitPoints": 1, "armorClass": 1,
                      "attackStrength": 0, "attackDice": 1, "blocks": ["north"],
                      "blockedText": "The wolf stands in the passage north." } ],
      "locations": [
        { "index": 1, "name": "Hall", "description": "A hall. A passage runs north.", "isDark": true,
          "darkDescription": "It's pitch dark. A draught comes from the north.",
          "monster": "wolf", "ground": ["lamp"], "connections": { "north": 2 } },
        { "index": 2, "name": "Out", "description": "Daylight.", "isExit": true, "connections": { "south": 1 } }
      ]
    }
    """;

    [Fact]
    public void ACreatureStandsInTheWayItBlocksAndTheWayStillShows()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "look");
        Assert.Contains("A draught comes from the north.", output.Text);
        Assert.Contains("Exits: north.", output.Text);

        output.Clear();
        engine.HandleInput(session, "north");
        Assert.Contains("Something huge is in the way.", output.Text);
        Assert.Equal(1, session.CurrentLocationId);
        Assert.Contains(events.Events, e => e.Type == EventType.BlockedMovement && e.Payload.GetValueOrDefault("by") == "wolf");
    }

    [Fact]
    public void LitTheCreatureIsNamedAndSlainItStandsAside()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);
        session.Character.Inventory.Add(engine.World.CreateItem("lamp")!);

        engine.HandleInput(session, "north");
        Assert.Contains("The wolf stands in the passage north.", output.Text);

        engine.HandleInput(session, "attack wolf");
        engine.HandleInput(session, "north");
        Assert.Equal(2, session.CurrentLocationId);
    }
}
