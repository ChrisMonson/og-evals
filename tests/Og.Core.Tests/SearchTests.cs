using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>"search" goes to the room first; if nothing answers, the place is described.</summary>
public class SearchTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "locations": [
        { "index": 1, "name": "Yard", "description": "A yard.", "connections": { "east": 2 },
          "interactions": [
            { "interactionId": "under-stone", "triggers": ["*search*"],
              "actions": [{ "action": "describe", "parameters": ["Under a loose stone, nothing but woodlice."] }] }
          ] },
        { "index": 2, "name": "Field", "description": "Reeds and mud.", "connections": { "west": 1 } }
      ]
    }
    """;

    [Fact]
    public void BareSearchReachesTheRoomFirst()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "search");

        Assert.Contains("woodlice", output.Text);
        Assert.Equal("under-stone", Assert.Single(events.Events, e => e.Type == EventType.InteractionFired).Payload["interaction"]);
    }

    [Fact]
    public void SearchingAPlaceWithNothingToSearchDescribesIt()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);
        engine.HandleInput(session, "east");
        output.Clear();

        engine.HandleInput(session, "search");

        Assert.Contains("Reeds and mud.", output.Text);
        Assert.DoesNotContain(events.Events, e => e.Type == EventType.UnparsedCommand);
    }
}
