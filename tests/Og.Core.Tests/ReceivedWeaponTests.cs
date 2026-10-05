namespace Og.Core.Tests;

/// <summary>A weapon or armour handed over by an interaction is equipped, as one taken from the ground is.</summary>
public class ReceivedWeaponTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "items": [ { "name": "sword", "description": "A sword.", "kind": "weapon", "attackBonus": 4, "attackDice": 8 },
                 { "name": "mail", "description": "A mail shirt.", "kind": "armor", "armorClassBonus": 3 } ],
      "locations": [ { "index": 1, "name": "Stall", "description": "A stall.",
        "interactions": [ { "interactionId": "sell", "triggers": ["buy sword"],
          "actions": [ { "action": "additems", "parameters": ["sword", "mail"] } ] } ] } ]
    }
    """;

    [Fact]
    public void AReceivedSwordIsHeldReadyAndArmourIsPutOn()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json, seed: 1);

        engine.HandleInput(session, "buy sword");

        Assert.Equal("sword", session.Character.Weapon?.Name);
        Assert.Equal("mail", session.Character.Armor?.Name);
        Assert.Contains("You received sword, and hold it ready.", output.Text);
        Assert.Contains("You received mail, and put it on.", output.Text);
    }
}
