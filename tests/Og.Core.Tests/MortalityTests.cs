using Og.Core.Runtime;

namespace Og.Core.Tests;

public class MortalityTests
{
    [Fact]
    public void DeathIsFinalForThatCharacter()
    {
        var (engine, _) = WorldFixture.NewEngine();
        var (alice, _) = WorldFixture.Enter(engine, "Alice");
        var character = alice.Character;

        WorldFixture.Kill(engine, alice);

        Assert.True(character.IsDead);
        Assert.True(alice.Over);
    }

    [Fact]
    public void DeadCharactersStopAcceptingInput()
    {
        var (engine, events) = WorldFixture.NewEngine();
        var (alice, _) = WorldFixture.Enter(engine, "Alice");

        WorldFixture.Kill(engine, alice);
        var countAfterDeath = events.Events.Count;

        engine.HandleInput(alice, "look");

        Assert.Equal(countAfterDeath, events.Events.Count);
    }

    [Fact]
    public void ANewLifeSharesNothingWithTheOldOne()
    {
        var (engine, _) = WorldFixture.NewEngine();
        var (first, _) = WorldFixture.Enter(engine, "Alice");

        engine.HandleInput(first, "east");
        engine.HandleInput(first, "search the camp");
        first.Character.Conditions.Add("blessed");

        var firstCharacter = first.Character;
        WorldFixture.Kill(engine, first);

        var (second, _) = WorldFixture.Enter(engine, "Alice");

        Assert.NotEqual(firstCharacter.Id, second.Character.Id);
        Assert.Empty(second.Character.Conditions);
        Assert.Empty(second.Character.FiredInteractions);
        Assert.Equal(engine.World.SpawnLocation, second.CurrentLocationId);
        Assert.DoesNotContain(second.Character.Inventory, item => item.Name == "shiny spoon");
    }

    [Fact]
    public void PossessionsAreLeftWhereTheCharacterFell()
    {
        var (engine, _) = WorldFixture.NewEngine();
        var (alice, _) = WorldFixture.Enter(engine, "Alice");

        engine.HandleInput(alice, "east");
        engine.HandleInput(alice, "search the camp");

        var deathLocation = engine.CurrentLocation(alice)!;
        WorldFixture.Kill(engine, alice);

        Assert.Contains(deathLocation.Items, item => item.Name == "shiny spoon");
        Assert.Contains(deathLocation.Items, item => item.Name == "dagger");
        Assert.Empty(alice.Character.Inventory);
        Assert.Null(alice.Character.Weapon);
    }

    [Fact]
    public void ANewLifeCanTakeWhatTheOldOneLeftBehind()
    {
        var (engine, _) = WorldFixture.NewEngine();
        var (alice, _) = WorldFixture.Enter(engine, "Alice");

        WorldFixture.Kill(engine, alice);
        var (bob, bobOut) = WorldFixture.Enter(engine, "Bob");
        engine.HandleInput(bob, "take dagger");

        // Bob's own dagger is already in hand, so the one Alice dropped goes in the pack.
        Assert.Contains("You take the dagger", bobOut.Text);
        Assert.Contains(bob.Character.Inventory, item => item.Name == "dagger");
    }

    [Fact]
    public void TheEventLogRecordsDeathAndBirthSeparately()
    {
        var (engine, events) = WorldFixture.NewEngine();
        var (alice, _) = WorldFixture.Enter(engine, "Alice");
        var firstId = alice.Character.Id;

        WorldFixture.Kill(engine, alice, "a goblin");
        var (second, _) = WorldFixture.Enter(engine, "Alice");

        var death = Assert.Single(events.Events, e => e.Type == EventType.Death);
        Assert.Equal(firstId, death.CharacterId);
        Assert.Equal("a goblin", death.Payload["cause"]);

        var births = events.Events.Where(e => e.Type == EventType.CharacterBorn).ToList();
        Assert.Equal(2, births.Count);
        Assert.Equal(second.Character.Id, births[1].CharacterId);
    }

    [Fact]
    public void NoEventCarriesAnythingLinkingTwoLives()
    {
        var (engine, events) = WorldFixture.NewEngine();
        var (alice, _) = WorldFixture.Enter(engine, "Alice");
        var firstId = alice.Character.Id;

        WorldFixture.Kill(engine, alice);
        var (second, _) = WorldFixture.Enter(engine, "Alice");
        engine.HandleInput(second, "look");

        // Nothing emitted after the new birth may mention the dead character's id.
        var afterBirth = events.Events
            .SkipWhile(e => e.CharacterId != second.Character.Id)
            .ToList();

        Assert.NotEmpty(afterBirth);
        Assert.DoesNotContain(afterBirth, e =>
            e.CharacterId == firstId || e.Payload.Values.Any(value => value.Contains(firstId)));
    }
}
