using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>enableinteraction and disableinteraction, at world and player scope.</summary>
public class EnableInteractionTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "locations": [
        { "index": 1, "name": "Hall", "description": "A hall.",
          "interactions": [
            { "interactionId": "door", "triggers": ["open door"], "disabled": true,
              "actions": [ { "action": "describe", "parameters": ["The door swings open."] } ] },
            { "interactionId": "lever", "triggers": ["pull lever"],
              "actions": [ { "action": "enableinteraction", "parameters": ["door"] },
                           { "action": "disableinteraction", "parameters": ["lever"] },
                           { "action": "describe", "parameters": ["Something clicks."] } ] },
            { "interactionId": "bell", "triggers": ["ring bell"], "scope": "Player",
              "actions": [ { "action": "disableinteraction", "parameters": ["bell"], "scope": "Player" },
                           { "action": "describe", "parameters": ["Ding."] } ] }
          ] }
      ]
    }
    """;

    [Fact]
    public void ADisabledInteractionCanBeEnabledAndAnotherDisabled()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "open door");
        Assert.DoesNotContain("swings open", output.Text);

        engine.HandleInput(session, "pull lever");
        engine.HandleInput(session, "open door");
        Assert.Contains("swings open", output.Text);

        output.Clear();
        engine.HandleInput(session, "pull lever");
        Assert.DoesNotContain("Something clicks.", output.Text);
    }

    [Fact]
    public void APlayerScopedInteractionIsDisabledForThatCharacterOnly()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "ring bell");
        engine.HandleInput(session, "ring bell");
        Assert.Single(output.Lines, line => line == "Ding.");

        WorldFixture.Kill(engine, session);
        var (next, nextOutput) = WorldFixture.Enter(engine);
        engine.HandleInput(next, "ring bell");
        Assert.Contains("Ding.", nextOutput.Text);
    }

    [Fact]
    public void TheValidatorCatchesTogglingAnUnknownInteraction()
    {
        var world = World.FromJson(Json.Replace("\"enableinteraction\", \"parameters\": [\"door\"]",
                                                "\"enableinteraction\", \"parameters\": [\"window\"]"));

        Assert.Contains(WorldValidator.Validate(world), issue => issue.Message.Contains("window"));
    }
}
