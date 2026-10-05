namespace Og.Core.Model;

/// <summary>
/// One life. Death is final and everything carried or flagged is lost.
/// </summary>
public sealed class Character : Creature
{
    public const int BaseArmorClass = 12;
    public const int BaseAttackStrength = 3;
    public const int StartingHitPoints = 20;

    /// <summary>Unique to this character.</summary>
    public string Id { get; }

    public DateTimeOffset BornAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DiedAt { get; private set; }
    public string? CauseOfDeath { get; private set; }

    public bool IsDead => DiedAt is not null;

    public int CurrentLocationId { get; set; }

    public Item? Weapon { get; set; }
    public Item? Armor { get; set; }

    /// <summary>
    /// Character-scoped state. All of it dies with the character: a new life starts
    /// with no flags, no quest progress, and nothing already fired.
    /// </summary>
    public HashSet<string> Conditions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DisabledInteractions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> FiredInteractions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Character(string id, string name)
    {
        Id = id;
        Name = name;
        MaxHitPoints = StartingHitPoints;
        HitPoints = StartingHitPoints;
    }

    public override int ArmorClass => BaseArmorClass + (Armor?.Definition.ArmorClassBonus ?? 0);
    public override int AttackStrength => BaseAttackStrength + (Weapon?.Definition.AttackBonus ?? 0);
    public override int AttackDice => Weapon?.Definition.AttackDice ?? 1;

    public bool HasKey => Inventory.Any(item => item.Definition.IsKey);
    public bool HasLight => AllPossessions().Any(item => item.Definition.IsLight);

    public void MarkDead(string cause)
    {
        DiedAt = DateTimeOffset.UtcNow;
        CauseOfDeath = cause;
    }

    /// <summary>
    /// Takes an item, equipping a weapon or armour if that slot is empty.
    /// True if it was equipped rather than packed.
    /// </summary>
    public bool Receive(Item item)
    {
        switch (item.Kind)
        {
            case ItemKind.Weapon when Weapon is null:
                Weapon = item;
                return true;

            case ItemKind.Armor when Armor is null:
                Armor = item;
                return true;

            default:
                Inventory.Add(item);
                return false;
        }
    }

    /// <summary>How an equipped item is said to be readied: "hold it ready" or "put it on".</summary>
    public static string Readied(Item item) => item.Kind == ItemKind.Armor ? "put it on" : "hold it ready";

    /// <summary>Everything carried or worn, for what the corpse leaves behind.</summary>
    public IEnumerable<Item> AllPossessions()
    {
        foreach (var item in Inventory)
        {
            yield return item;
        }

        if (Weapon is not null) yield return Weapon;
        if (Armor is not null) yield return Armor;
    }

    public Item? FindItemOrEquipped(string name)
    {
        if (FindItem(name) is { } carried)
        {
            return carried;
        }

        if (Weapon is not null && Weapon.Matches(name))
        {
            return Weapon;
        }

        return Armor is not null && Armor.Matches(name) ? Armor : null;
    }
}
