namespace Og.Core.Model;

public abstract class Creature
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int HitPoints { get; set; }
    public int MaxHitPoints { get; set; }
    public List<Item> Inventory { get; } = [];

    public abstract int ArmorClass { get; }
    public abstract int AttackStrength { get; }
    public abstract int AttackDice { get; }

    public bool IsAlive => HitPoints > 0;

    /// <summary>Resolves an incoming attack. Returns true if it connected.</summary>
    public bool ReceiveAttack(int attackRoll, int damage)
    {
        if (attackRoll < ArmorClass)
        {
            return false;
        }

        HitPoints = Math.Max(0, HitPoints - damage);
        return true;
    }

    public Item? FindItem(string name) =>
        Inventory.FirstOrDefault(item => item.Matches(name));

    /// <summary>
    /// Generic words that refer to whatever creature is present, for when it
    /// can't be seen or its name isn't known.
    /// </summary>
    private static readonly HashSet<string> AnyCreature = new(StringComparer.OrdinalIgnoreCase)
    {
        "creature", "monster", "it", "thing", "animal", "enemy", "foe", "brute", "something",
        "the creature", "the monster", "the thing", "the animal", "the enemy", "the foe", "the brute",
    };

    /// <summary>Whether this creature is what someone just named.</summary>
    public bool Matches(string name)
    {
        var wanted = name.Trim();

        if (wanted.Length == 0 || AnyCreature.Contains(wanted))
        {
            return true;
        }

        if (string.Equals(Name, wanted, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Name.Split(' ').Any(word =>
            string.Equals(word, wanted, StringComparison.OrdinalIgnoreCase));
    }
}
