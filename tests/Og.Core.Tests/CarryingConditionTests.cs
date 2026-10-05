using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>A carrying: condition holds while the character has the item.</summary>
public class CarryingConditionTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "startingItems": ["cheese", "dagger"],
      "items": [
        { "name": "cheese", "description": "A wedge of hard cheese." },
        { "name": "dagger", "description": "A dagger.", "kind": "weapon", "attackDice": 4 }
      ],
      "locations": [
        { "index": 1, "name": "Yard", "description": "A yard.",
          "interactions": [
            { "interactionId": "trade", "triggers": ["*cheese*"], "if": ["carrying:cheese"],
              "actions": [ { "action": "describe", "parameters": ["Traded."] } ] }
          ] }
      ]
    }
    """;

    [Fact]
    public void ACarryingConditionHoldsOnlyWhileTheItemIsHeld()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);

        Assert.True(engine.World.HasCondition(session, "carrying:cheese"));
        engine.HandleInput(session, "drop cheese");
        Assert.False(engine.World.HasCondition(session, "carrying:cheese"));

        // With the cheese on the ground, naming it no longer fires the interaction.
        engine.HandleInput(session, "give cheese to someone");
        Assert.DoesNotContain("Traded.", output.Text);
    }

    [Fact]
    public void AnEquippedItemCountsAsCarried()
    {
        var (engine, _, session, _) = WorldFixture.Start(Json);

        Assert.Equal("dagger", session.Character.Weapon?.Name);
        Assert.True(engine.World.HasCondition(session, "carrying:dagger"));
    }

    [Fact]
    public void TheValidatorCatchesACarryingConditionForAnItemThatDoesNotExist()
    {
        var world = World.FromJson(Json.Replace("\"carrying:cheese\"", "\"carrying:chese\""));

        Assert.Contains(WorldValidator.Validate(world), issue => issue.Message.Contains("carrying:chese"));
    }
}
