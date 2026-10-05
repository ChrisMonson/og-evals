using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>
/// "give X to Y" checks that X is carried, lets the room decide whether it is
/// taken, and records the attempt either way.
/// </summary>
public class GivingTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "startingItems": ["cheese", "sword"],
      "items": [
        { "name": "cheese", "description": "A wedge of hard cheese." },
        { "name": "sword", "description": "A sword.", "kind": "weapon", "attackBonus": 4, "attackDice": 8 },
        { "name": "token", "description": "A wooden token." }
      ],
      "monsters": [
        { "name": "guard", "description": "A guard.", "hitPoints": 6, "armorClass": 8,
          "attackStrength": 0, "attackDice": 2 }
      ],
      "locations": [
        { "index": 1, "name": "Yard", "description": "A yard.", "connections": { "east": 2 }, "monster": "guard",
          "interactions": [
            { "interactionId": "trade", "triggers": ["*cheese*"], "if": ["carrying:cheese"],
              "actions": [
                { "action": "describe", "parameters": ["The guard takes the cheese and hands you a token."] },
                { "action": "removeitems", "parameters": ["cheese"] },
                { "action": "additems", "parameters": ["token"] }
              ] }
          ] },
        { "index": 2, "name": "Field", "description": "An empty field.", "connections": { "west": 1 } }
      ]
    }
    """;

    [Fact]
    public void GivingToSomethingThatTakesItIsRecorded()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "give the cheese to the guard");

        var gave = Assert.Single(events.Events, e => e.Type == EventType.Gave);
        Assert.Equal("cheese", gave.Payload["item"]);
        Assert.Equal("guard", gave.Payload["to"]);
        Assert.Equal("yes", gave.Payload["taken"]);
        Assert.Contains("hands you a token", output.Text);
        Assert.Null(session.Character.FindItem("cheese"));
        Assert.NotNull(session.Character.FindItem("token"));
    }

    [Fact]
    public void GivingWhatYouDoNotHaveIsRefusedAndNotRecordedAsGiving()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "give bread to guard");

        Assert.Contains("You aren't carrying a bread.", output.Text);
        Assert.DoesNotContain(events.Events, e => e.Type == EventType.Gave);
    }

    [Fact]
    public void GivingWhereNobodyTakesItKeepsTheItemAndSaysSo()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);
        engine.HandleInput(session, "east");
        output.Clear();

        engine.HandleInput(session, "offer cheese");

        Assert.Contains("Nobody here takes the cheese.", output.Text);
        Assert.Equal("no", Assert.Single(events.Events, e => e.Type == EventType.Gave).Payload["taken"]);
        Assert.NotNull(session.Character.FindItem("cheese"));
    }

    [Theory]
    [InlineData("offer the guard the sword as payment to pass", "Nobody here takes the sword.")]
    [InlineData("give the guard the cheese", "hands you a token")]
    public void TheItemIsFoundWhereverItIsNamed(string command, string expected)
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, command);

        Assert.Contains(expected, output.Text);
        Assert.Single(events.Events, e => e.Type == EventType.Gave);
    }

    [Fact]
    public void TheFixtureIsValid() => Assert.True(WorldFixture.IsValid(Json));
}
