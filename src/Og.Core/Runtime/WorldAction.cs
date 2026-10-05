using Og.Core.Definitions;
using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>Action names as written in world JSON (case-insensitive).</summary>
public static class ActionKind
{
    public const string Describe = "describe";
    public const string AddItems = "additems";
    public const string RemoveItems = "removeitems";
    public const string ChangeLocation = "changelocation";
    public const string SetLocationDescription = "setlocationdescription";
    public const string EnableInteraction = "enableinteraction";
    public const string DisableInteraction = "disableinteraction";
    public const string AddCondition = "addcondition";
    public const string RemoveCondition = "removecondition";

    /// <summary>
    /// Sends a monster away alive, with what it carries, recorded as monster_left.
    /// Optional parameter: location id (default: the current one).
    /// </summary>
    public const string RemoveMonster = "removemonster";

    /// <summary>
    /// Leaves the world as the exit command does, subject to ExitNeedsKey.
    /// Optional parameter: text shown if it fails.
    /// </summary>
    public const string Escape = "escape";

    /// <summary>Rewrites the description of a location's monster: [location, text].</summary>
    public const string SetMonsterDescription = "setmonsterdescription";

    /// <summary>Puts items on the ground at a location: [location, item, ...].</summary>
    public const string PlaceItems = "placeitems";

    /// <summary>
    /// Spends [amount] (default 1) of the clock without moving. With none left,
    /// the session ends.
    /// </summary>
    public const string SpendTime = "spendtime";

    /// <summary>
    /// Ends the session without an escape, recorded as game_ended with [reason].
    /// Later actions do not run.
    /// </summary>
    public const string EndGame = "endgame";
}

/// <summary>Everything an interaction can be told to do.</summary>
public static class ActionKinds
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ActionKind.Describe, ActionKind.AddItems, ActionKind.RemoveItems,
        ActionKind.ChangeLocation, ActionKind.SetLocationDescription,
        ActionKind.EnableInteraction, ActionKind.DisableInteraction,
        ActionKind.AddCondition, ActionKind.RemoveCondition, ActionKind.RemoveMonster,
        ActionKind.Escape, ActionKind.SetMonsterDescription, ActionKind.PlaceItems, ActionKind.SpendTime, ActionKind.EndGame
    };
}

public sealed class WorldAction(ActionDefinition definition)
{
    public string Kind { get; } = definition.Action.Trim().ToLowerInvariant();

    /// <summary>As it was written, for saying back to whoever wrote it.</summary>
    public string AsWritten { get; } = definition.Action.Trim();
    public string[] Parameters { get; } = definition.Parameters;
    public Scope Scope { get; } = definition.Scope;

    public string FirstParameter => Parameters.Length > 0 ? Parameters[0] : "";

    public int FirstParameterAsInt =>
        Parameters.Length > 0 && int.TryParse(Parameters[0], out var value) ? value : 0;
}
