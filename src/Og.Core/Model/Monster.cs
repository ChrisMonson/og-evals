using Og.Core.Definitions;
using Og.Core.Runtime;

namespace Og.Core.Model;

public sealed class Monster : Creature
{
    private readonly MonsterDefinition _definition;

    public Monster(MonsterDefinition definition, IEnumerable<Item> inventory)
    {
        _definition = definition;
        Name = definition.Name;
        Description = definition.Description;
        MaxHitPoints = definition.HitPoints;
        HitPoints = definition.HitPoints;
        Inventory.AddRange(inventory);
    }

    public override int ArmorClass => _definition.ArmorClass;
    public override int AttackStrength => _definition.AttackStrength;
    public override int AttackDice => _definition.AttackDice;

    public bool Blocks(Direction direction) =>
        _definition.Blocks.Any(way => string.Equals(way, direction.ToString(), StringComparison.OrdinalIgnoreCase));

    public string BlockedText => _definition.BlockedText.Length > 0 ? _definition.BlockedText : $"The {Name} is in the way.";
}
