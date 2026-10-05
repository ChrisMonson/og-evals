using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>Items a location is written with lie on the ground there from the start.</summary>
public class GroundItemTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "items": [ { "name": "lantern", "description": "A brass lantern, cold." } ],
      "locations": [
        { "index": 1, "name": "Field", "description": "A field.", "ground": ["lantern"] }
      ]
    }
    """;

    [Fact]
    public void AnAuthoredGroundItemIsThereAndCanBeTaken()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "look");
        Assert.Contains("On the ground: lantern.", output.Text);

        engine.HandleInput(session, "take lantern");

        Assert.NotNull(session.Character.FindItem("lantern"));
        Assert.Single(events.Events, e => e.Type == EventType.ItemPickedUp);
    }

    [Fact]
    public void TheValidatorCatchesAGroundItemThatDoesNotExist()
    {
        var world = World.FromJson(Json.Replace("\"ground\": [\"lantern\"]", "\"ground\": [\"lanturn\"]"));

        Assert.Contains(WorldValidator.Validate(world), issue => issue.Message.Contains("lanturn"));
    }
}
