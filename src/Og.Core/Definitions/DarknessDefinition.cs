namespace Og.Core.Definitions;

/// <summary>
/// What it is like in a dark place without a light. Darkness hinders; it does not
/// stop anyone. A player can still walk, fight and find the way out -- at the risk
/// of a fall on each dark place entered, and with every blow struck at a penalty.
/// </summary>
public sealed class DarknessDefinition
{
    /// <summary>Said instead of the place's description.</summary>
    public string Description { get; set; } = "It is pitch dark.";

    /// <summary>Said instead of a creature's description, if one is there.</summary>
    public string Creature { get; set; } = "Something is in here with you.";

    /// <summary>Chance, in percent, of a fall on entering a dark place.</summary>
    public int FallChance { get; set; }

    /// <summary>A fall does 1 to this much damage.</summary>
    public int FallDamage { get; set; } = 4;

    /// <summary>Said on a fall, before the damage.</summary>
    public string FallText { get; set; } = "You stumble in the dark and fall.";

    /// <summary>Taken off every attack roll made in the dark.</summary>
    public int AttackPenalty { get; set; }

    /// <summary>Said, in the dark, when a creature blocks the way.</summary>
    public string Blocked { get; set; } = "Something is in the way.";
}
