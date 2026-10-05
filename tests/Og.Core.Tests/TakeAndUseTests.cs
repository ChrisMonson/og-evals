using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>
/// Taking from the ground and from a creature, equipping on take, and using one
/// thing on another.
/// </summary>
public class TakeAndUseTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "startingItems": ["bread"],
      "items": [
        { "name": "bread", "description": "A loaf." },
        { "name": "iron key", "description": "An iron key.", "isKey": true },
        { "name": "pebble", "description": "A pebble." },
        { "name": "sword", "description": "A sword.", "kind": "weapon", "attackBonus": 4, "attackDice": 8 },
        { "name": "dagger", "description": "A dagger.", "kind": "weapon", "attackDice": 4 }
      ],
      "monsters": [
        { "name": "guard", "description": "A guard with a key.", "hitPoints": 4, "armorClass": 8,
          "attackStrength": 0, "attackDice": 2, "inventory": ["iron key"] }
      ],
      "locations": [
        { "index": 1, "name": "Yard", "description": "A yard.", "connections": { "east": 2 },
          "monster": "guard", "ground": ["pebble", "sword", "dagger"],
          "interactions": [
            { "interactionId": "grab-answer", "triggers": ["*key from*"],
              "actions": [{ "action": "describe", "parameters": ["'Not a chance.'"] }] }
          ] },
        { "index": 2, "name": "Gate", "description": "A gate.", "connections": { "west": 1 },
          "interactions": [
            { "interactionId": "gate-open", "triggers": ["*gate*", "*unlock*"],
              "actions": [{ "action": "escape", "parameters": ["The gate is locked."] }] }
          ] }
      ]
    }
    """;

    [Fact]
    public void TakingFromACreatureIsUnderstoodRefusedAndRecorded()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "take the iron key from the guard");

        var attempt = Assert.Single(events.Events, e => e.Type == EventType.TriedTaking);
        Assert.Equal("iron key", attempt.Payload["item"]);
        Assert.Equal("guard", attempt.Payload["from"]);
        Assert.Null(session.Character.FindItem("iron key"));
        // The room had its own answer, so that is what was said.
        Assert.Contains("Not a chance.", output.Text);
    }

    [Fact]
    public void TakingFromACreatureWithNoRoomAnswerSaysWhyItFailed()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "grab key off guard");
        engine.HandleInput(session, "take pebble off guard");

        Assert.Contains("won't let go of the iron key", output.Text);
        Assert.Contains("The guard has no pebble.", output.Text);
        Assert.Equal(2, events.Events.Count(e => e.Type == EventType.TriedTaking));
    }

    [Fact]
    public void BareTakeOfSomethingACreatureHoldsIsTheSameAttempt()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "take iron key");

        var attempt = Assert.Single(events.Events, e => e.Type == EventType.TriedTaking);
        Assert.Equal("guard", attempt.Payload["from"]);
        Assert.Contains("won't let go of the iron key", output.Text);
        Assert.DoesNotContain("There is no", output.Text);
    }

    [Fact]
    public void BareTakeOfSomethingNobodyHasIsStillNotHere()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "take token");

        Assert.Contains("There is no token here to take.", output.Text);
        Assert.DoesNotContain(events.Events, e => e.Type == EventType.TriedTaking);
    }

    [Fact]
    public void AWeaponPickedUpWithEmptyHandsIsHeldReady()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "take sword");

        Assert.Equal("sword", session.Character.Weapon?.Name);
        Assert.Null(session.Character.FindItem("sword"));
        Assert.Contains("hold it ready", output.Text);
    }

    [Fact]
    public void ASecondWeaponGoesInThePack()
    {
        var (engine, _, session, _) = WorldFixture.Start(Json);

        engine.HandleInput(session, "take sword");
        engine.HandleInput(session, "take dagger");

        Assert.Equal("sword", session.Character.Weapon?.Name);
        Assert.NotNull(session.Character.FindItem("dagger"));
    }

    [Fact]
    public void TakingSomethingOnTheGroundStillWorks()
    {
        var (engine, _, session, _) = WorldFixture.Start(Json);

        engine.HandleInput(session, "take the pebble");

        Assert.NotNull(session.Character.FindItem("pebble"));
    }

    [Fact]
    public void UsingSomethingOnSomethingFindsTheItemAndLetsTheRoomAnswer()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);
        engine.HandleInput(session, "east");
        session.Character.Inventory.Add(engine.World.CreateItem("iron key")!);
        output.Clear();

        engine.HandleInput(session, "use the iron key on the gate");

        Assert.DoesNotContain("aren't carrying", output.Text);
        Assert.Single(events.Events, e => e.Type == EventType.Escaped);
    }

    [Fact]
    public void UsingSomethingOnSomethingWithNoAnswerSaysSo()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);
        engine.HandleInput(session, "east");
        output.Clear();

        engine.HandleInput(session, "use bread on the wall");

        Assert.Contains("Nothing happens when you use the bread on the wall.", output.Text);
        Assert.NotNull(session.Character.FindItem("bread"));
    }

    [Fact]
    public void TheFixtureIsValid() => Assert.True(WorldFixture.IsValid(Json));
}
