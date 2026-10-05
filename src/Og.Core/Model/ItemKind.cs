namespace Og.Core.Model;

public enum ItemKind
{
    Misc,
    Weapon,
    Armor,
    Consumable,

    /// <summary>Money. Carrying some is what lets <c>buy</c> pay.</summary>
    Currency
}
