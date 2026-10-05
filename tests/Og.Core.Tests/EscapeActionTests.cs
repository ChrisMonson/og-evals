using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>The escape action leaves the world as the exit command does, subject to ExitNeedsKey.</summary>
public class EscapeActionTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "items": [ { "name": "iron key", "description": "An iron key.", "isKey": true } ],
      "locations": [
        { "index": 1, "name": "Gate", "description": "A gate.", "isExit": true,
          "interactions": [
            { "interactionId": "gate-open", "triggers": ["*gate*", "*unlock*"],
              "actions": [{ "action": "escape", "parameters": ["The gate is locked."] }] }
          ] }
      ]
    }
    """;

    [Fact]
    public void AnEscapeActionLeavesOnlyWithAKey()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "open the gate");
        Assert.Contains("The gate is locked.", output.Text);
        Assert.DoesNotContain(events.Events, e => e.Type == EventType.Escaped);

        session.Character.Inventory.Add(engine.World.CreateItem("iron key")!);
        engine.HandleInput(session, "unlock it");
        Assert.Single(events.Events, e => e.Type == EventType.Escaped);
    }
}
