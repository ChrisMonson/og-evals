namespace Og.Core.Tests;

/// <summary>Equipping what is already in hand says so, rather than that it is not carried.</summary>
public class EquipHeldTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "items": [ { "name": "sword", "description": "A sword.", "kind": "weapon", "attackBonus": 4, "attackDice": 8 } ],
      "locations": [ { "index": 1, "name": "Post", "description": "A guard post.", "ground": ["sword"] } ]
    }
    """;

    [Theory]
    [InlineData("equip sword")]
    [InlineData("equip the sword")]
    [InlineData("wield sword")]
    public void EquippingTheHeldWeaponSaysItIsHeld(string command)
    {
        var (engine, _, session, output) = WorldFixture.Start(Json, seed: 1);

        engine.HandleInput(session, "take sword");
        output.Clear();
        engine.HandleInput(session, command);

        Assert.Contains("already holding the sword", output.Text);
        Assert.NotNull(session.Character.Weapon);
    }
}
