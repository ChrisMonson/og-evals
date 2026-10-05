using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>Interactions that change a monster: removemonster and setmonsterdescription.</summary>
public class MonsterActionTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "startingItems": ["bread", "apple"],
      "items": [
        { "name": "bread", "description": "A loaf." },
        { "name": "apple", "description": "An apple." },
        { "name": "key", "description": "An iron key.", "isKey": true }
      ],
      "monsters": [
        { "name": "guard", "description": "A guard turning a key on a string.", "hitPoints": 6,
          "armorClass": 8, "attackStrength": 0, "attackDice": 2, "inventory": ["key"] }
      ],
      "locations": [
        { "index": 1, "name": "Yard", "description": "A yard.", "monster": "guard",
          "interactions": [
            { "interactionId": "eat", "triggers": ["*apple*"], "if": ["carrying:apple"],
              "actions": [
                { "action": "removeitems", "parameters": ["apple"] },
                { "action": "setmonsterdescription", "parameters": ["1", "A guard, eating."] }
              ] },
            { "interactionId": "leave", "triggers": ["*bread*"], "if": ["carrying:bread"],
              "actions": [
                { "action": "removeitems", "parameters": ["bread"] },
                { "action": "removemonster", "parameters": [] }
              ] }
          ] }
      ]
    }
    """;

    [Fact]
    public void AMonstersDescriptionCanChange()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "give apple to guard");
        output.Clear();
        engine.HandleInput(session, "look");

        Assert.Contains("A guard, eating.", output.Text);
        Assert.DoesNotContain("turning a key", output.Text);
    }

    [Fact]
    public void AMonsterCanBeSentAwayAliveWithWhatItCarries()
    {
        var (engine, events, session, _) = WorldFixture.Start(Json);

        engine.HandleInput(session, "give bread to guard");

        var location = engine.CurrentLocation(session)!;
        Assert.Null(location.Monster);
        Assert.Equal("guard", Assert.Single(events.Events, e => e.Type == EventType.MonsterLeft).Payload["monster"]);
        Assert.DoesNotContain(events.Events, e => e.Type == EventType.MonsterSlain);
        Assert.DoesNotContain(location.Items, item => item.Name == "key");
    }

    [Fact]
    public void TheValidatorCatchesSendingAwayAMonsterFromNowhere()
    {
        var world = World.FromJson(Json.Replace(
            "\"removemonster\", \"parameters\": []", "\"removemonster\", \"parameters\": [\"9\"]"));

        Assert.Contains(WorldValidator.Validate(world), issue => issue.Message.Contains("location 9"));
    }

    [Fact]
    public void TheFixtureIsValid() => Assert.True(WorldFixture.IsValid(Json));
}
