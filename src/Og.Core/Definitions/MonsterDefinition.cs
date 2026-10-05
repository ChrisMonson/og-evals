namespace Og.Core.Definitions;

public sealed class MonsterDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int HitPoints { get; set; }
    public int ArmorClass { get; set; }
    public int AttackStrength { get; set; }
    public int AttackDice { get; set; }
    public string[] Inventory { get; set; } = [];

    /// <summary>
    /// Directions this monster blocks while alive. The exit is still listed, but
    /// moving that way is refused with BlockedText.
    /// </summary>
    public string[] Blocks { get; set; } = [];

    /// <summary>Said when someone tries to go a way it blocks.</summary>
    public string BlockedText { get; set; } = "";
}
