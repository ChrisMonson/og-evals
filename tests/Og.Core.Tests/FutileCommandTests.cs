using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>
/// The engine claims a command only when it did something with it. A verb that
/// came to nothing carries on to the place the player is standing in — which is
/// how a room gets to answer "take the belongings" or "inspect the stones".
/// </summary>
public class FutileCommandTests
{
    private const string Room = """
    {
      "spawnLocation": 1,
      "items": [ { "name": "lamp", "description": "A lamp." } ],
      "locations": [
        {
          "index": 1, "name": "A", "description": "A room with belongings scattered about.",
          "isExit": true,
          "interactions": [
            {
              "interactionId": "belongings",
              "triggers": ["*belongings*"],
              "actions": [ { "action": "describe", "parameters": ["Rags, mostly, and a bent spoon."] } ]
            },
            {
              "interactionId": "lamp-socket",
              "triggers": ["*lamp*"],
              "actions": [ { "action": "describe", "parameters": ["The lamp fits the bracket on the wall."] } ]
            }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void AVerbThatCameToNothingReachesTheRoom()
    {
        var (engine, _, session, output) = WorldFixture.Start(Room, seed: 3);

        engine.HandleInput(session, "take the belongings");

        Assert.Contains("Rags, mostly", output.Text);
        Assert.DoesNotContain("no belongings here", output.Text);
    }

    [Fact]
    public void SoDoesInspect()
    {
        var (engine, _, session, output) = WorldFixture.Start(Room, seed: 3);

        engine.HandleInput(session, "inspect the belongings");

        Assert.Contains("Rags, mostly", output.Text);
    }

    [Fact]
    public void AndUseOnSomethingTheEngineCannotUse()
    {
        var (engine, _, session, output) = WorldFixture.Start(Room, seed: 3);
        session.Character.Inventory.Add(engine.World.CreateItem("lamp")!);

        engine.HandleInput(session, "use the lamp");

        Assert.Contains("fits the bracket", output.Text);
    }

    [Fact]
    public void AVerbThatDidSomethingDoesNotReachTheRoom()
    {
        var (engine, _, session, output) = WorldFixture.Start(Room, seed: 3);
        engine.CurrentLocation(session)!.Items.Add(engine.World.CreateItem("lamp")!);

        engine.HandleInput(session, "take lamp");

        Assert.Contains("You take the lamp", output.Text);
        Assert.DoesNotContain("fits the bracket", output.Text);
    }

    [Fact]
    public void WhenNothingHasAnAnswerTheEngineSaysWhatItWouldHaveSaid()
    {
        var (engine, _, session, output) = WorldFixture.Start(Room, seed: 3);

        engine.HandleInput(session, "take the moon");

        // The leading article is dropped from the name.
        Assert.Contains("There is no moon here to take.", output.Text);
    }

    [Fact]
    public void AndItIsRecordedAsHavingHadNoEffect()
    {
        var (engine, events, session, _) = WorldFixture.Start(Room, seed: 3);

        engine.HandleInput(session, "take the moon");

        var recorded = Assert.Single(events.Events, e => e.Type == EventType.UnparsedCommand);
        Assert.Equal("no effect", recorded.Payload["kind"]);
        Assert.Equal("take the moon", recorded.Payload["text"]);
    }

    [Fact]
    public void SomethingThatIsNotAVerbAtAllIsRecordedDifferently()
    {
        var (engine, events, session, _) = WorldFixture.Start(Room, seed: 3);

        engine.HandleInput(session, "ponder the infinite");

        var recorded = Assert.Single(events.Events, e => e.Type == EventType.UnparsedCommand);
        Assert.Equal("unrecognised", recorded.Payload["kind"]);
    }

    [Fact]
    public void WalkingIntoAWallIsRecordedAndStillReachesTheRoom()
    {
        var (engine, events, session, output) = WorldFixture.Start(Room, seed: 3);

        engine.HandleInput(session, "north");

        Assert.Contains(events.Events, e => e.Type == EventType.BlockedMovement);
        Assert.Contains("You can't go that way.", output.Text);
    }

    [Fact]
    public void ExitWithoutAKeyIsRecordedRatherThanSwallowed()
    {
        var (engine, events, session, _) = WorldFixture.Start(Room, seed: 3);

        engine.HandleInput(session, "exit");

        var recorded = Assert.Single(events.Events, e => e.Type == EventType.UnparsedCommand);
        Assert.Equal("no effect", recorded.Payload["kind"]);
        Assert.Contains("key", recorded.Payload["refusal"]);
    }
}
