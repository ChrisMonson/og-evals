namespace Og.Core.Tests;

/// <summary>Common phrasings resolve to the intended target.</summary>
public class NameMatchingTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "items": [
        { "name": "health potion", "description": "Cloudy red.", "kind": "consumable", "healAmount": 10 },
        { "name": "shiny spoon", "description": "A spoon." }
      ],
      "monsters": [
        { "name": "stone giant", "description": "A giant.", "hitPoints": 30, "armorClass": 5,
          "attackStrength": 0, "attackDice": 1 }
      ],
      "locations": [
        {
          "index": 1, "name": "A", "description": "A passage. Something shifts.",
          "monster": "stone giant",
          "interactions": [
            { "interactionId": "floor", "triggers": ["*giant*", "*floor*"],
              "actions": [ { "action": "describe", "parameters": ["Bones, and dust."] } ] }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void NamingWhatYouAreFightingAttacksIt()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json, seed: 11);

        engine.HandleInput(session, "attack giant");

        Assert.True(output.Text.Contains("You hit") || output.Text.Contains("You missed"),
            $"expected a fight, got: {output.Text}");
        Assert.DoesNotContain("Bones, and dust", output.Text);
    }

    [Fact]
    public void AndSoDoesNamingItInFull()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json, seed: 11);

        engine.HandleInput(session, "attack the stone giant");

        Assert.True(output.Text.Contains("You hit") || output.Text.Contains("You missed"));
    }

    [Fact]
    public void AskingTheRoomAboutItStillDescribesIt()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json, seed: 11);

        engine.HandleInput(session, "look at the floor");

        Assert.Contains("Bones, and dust", output.Text);
    }

    [Fact]
    public void AttackingSomethingThatIsNotThereReachesTheRoomInstead()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json, seed: 11);

        engine.HandleInput(session, "attack the floor");

        Assert.Contains("Bones, and dust", output.Text);
    }

    [Fact]
    public void HalfANameIsEnoughForSomethingYouCarry()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json, seed: 11);
        session.Character.Inventory.Add(engine.World.CreateItem("health potion")!);
        session.Character.HitPoints = 5;

        engine.HandleInput(session, "use potion");

        Assert.Contains("You used the health potion", output.Text);
        Assert.Equal(15, session.Character.HitPoints);
        Assert.Null(session.Character.FindItem("potion"));
    }

    [Fact]
    public void AndForLookingAtIt()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json, seed: 11);
        session.Character.Inventory.Add(engine.World.CreateItem("shiny spoon")!);

        engine.HandleInput(session, "inspect spoon");

        Assert.Contains("A spoon.", output.Text);
    }
}
