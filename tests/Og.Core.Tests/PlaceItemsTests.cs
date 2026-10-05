using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>The placeitems action puts items on the ground at a location.</summary>
public class PlaceItemsTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "items": [ { "name": "pouch", "description": "A coin pouch." } ],
      "locations": [
        { "index": 1, "name": "Road", "description": "A road.",
          "interactions": [
            { "interactionId": "flee", "triggers": ["scare*"],
              "actions": [ { "action": "placeitems", "parameters": ["1", "pouch"] },
                           { "action": "describe", "parameters": ["Someone runs off, dropping a pouch."] } ] }
          ] }
      ]
    }
    """;

    [Fact]
    public void PlacedItemsLieOnTheGroundAndCanBeTaken()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "scare them");
        engine.HandleInput(session, "look");
        Assert.Contains("On the ground: pouch.", output.Text);

        engine.HandleInput(session, "take pouch");
        Assert.NotNull(session.Character.FindItem("pouch"));
    }

    [Fact]
    public void TheValidatorChecksTheLocationAndItems()
    {
        var broken = Json.Replace("[\"1\", \"pouch\"]", "[\"9\", \"crown\"]");
        var issues = WorldValidator.Validate(World.FromJson(broken)).Select(issue => issue.Message).ToList();

        Assert.Contains(issues, message => message.Contains("location '9'"));
        Assert.Contains(issues, message => message.Contains("'crown'"));
    }
}
